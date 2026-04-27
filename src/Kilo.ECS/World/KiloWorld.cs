using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using System.Collections.Generic;

namespace Kilo.ECS;

/// <summary>
/// The main ECS container. All game code and plugins should use this type exclusively.
/// </summary>
public sealed class KiloWorld : IDisposable
{
    internal readonly EntityStore _store;
    private Dictionary<Type, object>? _resources;
    private uint _currentTick;

    // Deferred command buffer
    private List<Action<KiloWorld>>? _deferredCommands;

    // Observer system
    private Dictionary<Type, List<Delegate>>? _observers;
    private HashSet<(Type triggerType, ulong entityId)>? _activeTriggers;

    // Change tracking — per (entity, component type)
    private HashSet<(ulong entityId, ulong componentId)>? _changedComponents;
    private HashSet<(ulong entityId, ulong componentId)>? _addedComponents;

    // By-ID component type registry (static, shared across all worlds)
    private static readonly Dictionary<ulong, Action<Entity>> _removeByComponentId = [];
    private static readonly Dictionary<ulong, Func<Entity, bool>> _hasByComponentId = [];
    private static readonly Dictionary<ulong, Func<Entity, object>> _getBoxedByComponentId = [];
    private static readonly Dictionary<ulong, Action<Entity, object>> _setBoxedByComponentId = [];
    private static readonly Dictionary<ulong, Type> _componentIdToType = [];
    private static readonly Dictionary<Type, ulong> _typeToComponentId = [];
    private static readonly EntityStore _registryStore = new(PidType.UsePidAsId);

    private Dictionary<Type, object> Resources => _resources ??= new Dictionary<Type, object>();

    /// <summary>Create a new world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloWorld(ulong maxComponentId = 256) => _store = new EntityStore(PidType.UsePidAsId);

    // ── Properties ───────────────────────────────────────────

    public uint CurrentTick
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _currentTick;
    }

    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _store.Count;
    }

    /// <summary>Advance the world tick and clear per-frame change markers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Update()
    {
        ClearChanged();
        return ++_currentTick;
    }

    // ── Component Type Registry ─────────────────────────────

    /// <summary>
    /// Register a component type for by-ID operations.
    /// Returns a stable ID usable with <see cref="Unset(EntityId, ulong)"/> and <see cref="Has(EntityId, ulong)"/>.
    /// Call once at startup for each component type that needs by-ID access.
    /// </summary>
    public static ulong RegisterComponentType<T>() where T : struct, IComponent
    {
        var type = typeof(T);
        if (_typeToComponentId.TryGetValue(type, out var existingId))
            return existingId;
        // Discover Friflo's StructIndex — same ID used in ComponentInfo and events
        var temp = _registryStore.CreateEntity();
        temp.AddComponent<T>();
        ulong id = 0;
        foreach (var ct in temp.Archetype.ComponentTypes) { id = (ulong)ct.StructIndex; break; }
        temp.DeleteEntity();
        _typeToComponentId[type] = id;
        _componentIdToType[id] = type;
        _removeByComponentId[id] = e => e.RemoveComponent<T>();
        _hasByComponentId[id] = e => e.HasComponent<T>();
        _getBoxedByComponentId[id] = e => e.GetComponent<T>(); // boxes struct → object
        _setBoxedByComponentId[id] = (e, obj) => e.AddComponent((T)obj); // unboxes
        return id;
    }

    /// <summary>
    /// Lazily discover and cache the Friflo StructIndex for component type T.
    /// This is the same ID used in <see cref="ComponentInfo.Id"/> and event callbacks.
    /// </summary>
    private static ulong GetComponentId<T>() where T : struct, IComponent
    {
        var type = typeof(T);
        if (_typeToComponentId.TryGetValue(type, out var id))
            return id;
        return RegisterComponentType<T>();
    }

    // ── Resource Management ─────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResource<T>() where T : notnull => (T)Resources[typeof(T)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasResource<T>() where T : notnull => _resources != null && _resources.ContainsKey(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddResource<T>(T resource) where T : notnull => Resources[typeof(T)] = resource;

    /// <summary>Remove a resource by type. Bevy's <c>world.remove_resource::&lt;T&gt;()</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveResource<T>() where T : notnull => _resources?.Remove(typeof(T));

    // ── Events (Bevy's Events<T>) ─────────────────────────────

    /// <summary>Get or create the event bus for type <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEvents<T> Events<T>() where T : struct
    {
        if (!HasResource<KiloEvents<T>>())
            AddResource(new KiloEvents<T>());
        return GetResource<KiloEvents<T>>();
    }

    /// <summary>Send an event. Shortcut for <c>Events&lt;T&gt;().Send()</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SendEvent<T>(T evt) where T : struct => Events<T>().Send(evt);

    /// <summary>Read events from the current frame. Shortcut for <c>Events&lt;T&gt;().Read()</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> ReadEvents<T>() where T : struct => Events<T>().Read();

    // ── Events ───────────────────────────────────────────────

    public event Action<KiloWorld, EntityId>? OnEntityCreated;
    public event Action<KiloWorld, EntityId>? OnEntityDeleted;
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentSet;
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentUnset;
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentAdded;

    private bool _eventsWired;

    internal void WireEvents()
    {
        if (_eventsWired) return;
        _eventsWired = true;

        _store.OnEntityCreate += ev =>
        {
            OnEntityCreated?.Invoke(this, new EntityId((ulong)ev.Entity.Id));
        };
        _store.OnEntityDelete += ev =>
        {
            OnEntityDeleted?.Invoke(this, new EntityId((ulong)ev.Entity.Id));
        };
        _store.OnComponentAdded += ev =>
        {
            var ci = new ComponentInfo((ulong)ev.ComponentType.StructIndex, ev.ComponentType.StructSize);
            // Auto-track added components (Bevy's Added<T> filter)
            _addedComponents ??= [];
            _addedComponents.Add(((ulong)ev.EntityId, (ulong)ev.ComponentType.StructIndex));
            OnComponentAdded?.Invoke(this, new EntityId((ulong)ev.EntityId), ci);
            OnComponentSet?.Invoke(this, new EntityId((ulong)ev.EntityId), ci);
        };
        _store.OnComponentRemoved += ev =>
        {
            var ci = new ComponentInfo((ulong)ev.ComponentType.StructIndex, ev.ComponentType.StructSize);
            OnComponentUnset?.Invoke(this, new EntityId((ulong)ev.EntityId), ci);
        };
    }

    // ── Entity Creation ──────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity(ulong id = 0)
    {
        if (id == 0)
        {
            var e = _store.CreateEntity();
            return new KiloEntity(e, this);
        }
        return new KiloEntity(_store.GetEntityById((int)id), this);
    }

    /// <summary>Create an entity with an initial component of type T.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity<T>() where T : struct, IComponent
    {
        var e = _store.CreateEntity();
        e.AddComponent<T>();
        return new KiloEntity(e, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity(string name)
    {
        var e = _store.CreateEntity();
        e.AddComponent(new EntityName(name));
        return new KiloEntity(e, this);
    }

    // ── Entity Lifecycle ─────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete(EntityId entity)
    {
        var e = _store.GetEntityById((int)entity.Value);
        DeleteChildren(e);
        e.DeleteEntity();
    }

    private void DeleteChildren(Entity parent)
    {
        var childIds = parent.ChildIds;
        if (childIds.IsEmpty) return;
        foreach (var childId in childIds)
        {
            var child = _store.GetEntityById(childId);
            DeleteChildren(child);
            child.DeleteEntity();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Exists(EntityId entity)
    {
        var e = _store.GetEntityById((int)entity.Value);
        return !e.IsNull;
    }

    // ── Component Operations (generic) ───────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(EntityId entity, T component = default) where T : struct, IComponent
    {
        var e = _store.GetEntityById((int)entity.Value);
        e.AddComponent(component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unset<T>(EntityId entity) where T : struct, IComponent
    {
        var e = _store.GetEntityById((int)entity.Value);
        e.RemoveComponent<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>(EntityId entity) where T : struct, IComponent
    {
        var e = _store.GetEntityById((int)entity.Value);
        return e.HasComponent<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>(EntityId entity) where T : struct, IComponent
    {
        var e = _store.GetEntityById((int)entity.Value);
        return ref e.GetComponent<T>();
    }

    // ── Component Operations (by ID) ─────────────────────────

    /// <summary>
    /// Remove a component by its registered ID.
    /// Requires prior registration via <see cref="RegisterComponentType{T}"/>.
    /// </summary>
    public void Unset(EntityId entity, ulong componentId)
    {
        if (!_removeByComponentId.TryGetValue(componentId, out var remove))
            throw new ArgumentException(
                $"Component ID {componentId} not registered. Call KiloWorld.RegisterComponentType<T>() first.",
                nameof(componentId));
        remove(_store.GetEntityById((int)entity.Value));
    }

    /// <summary>
    /// Check if an entity has a component by its registered ID.
    /// Requires prior registration via <see cref="RegisterComponentType{T}"/>.
    /// </summary>
    public bool Has(EntityId entity, ulong componentId)
    {
        if (!_hasByComponentId.TryGetValue(componentId, out var has))
            throw new ArgumentException(
                $"Component ID {componentId} not registered. Call KiloWorld.RegisterComponentType<T>() first.",
                nameof(componentId));
        return has(_store.GetEntityById((int)entity.Value));
    }

    /// <summary>
    /// Get a component by its registered ID (boxed).
    /// Requires prior registration via <see cref="RegisterComponentType{T}"/>.
    /// Note: this boxes the component struct. For hot paths, prefer the generic <c>Get&lt;T&gt;()</c>.
    /// </summary>
    public object Get(EntityId entity, ulong componentId)
    {
        if (!_getBoxedByComponentId.TryGetValue(componentId, out var get))
            throw new ArgumentException(
                $"Component ID {componentId} not registered. Call KiloWorld.RegisterComponentType<T>() first.",
                nameof(componentId));
        return get(_store.GetEntityById((int)entity.Value));
    }

    /// <summary>
    /// Set a component by its registered ID (boxed).
    /// Requires prior registration via <see cref="RegisterComponentType{T}"/>.
    /// Note: this unboxes the component struct. For hot paths, prefer the generic <c>Set&lt;T&gt;()</c>.
    /// </summary>
    public void Set(EntityId entity, ulong componentId, object component)
    {
        if (!_setBoxedByComponentId.TryGetValue(componentId, out var set))
            throw new ArgumentException(
                $"Component ID {componentId} not registered. Call KiloWorld.RegisterComponentType<T>() first.",
                nameof(componentId));
        set(_store.GetEntityById((int)entity.Value), component);
    }

    /// <summary>Resolve the <see cref="Type"/> for a registered component ID.</summary>
    public static Type GetComponentType(ulong componentId)
    {
        if (!_componentIdToType.TryGetValue(componentId, out var type))
            throw new ArgumentException(
                $"Component ID {componentId} not registered. Call KiloWorld.RegisterComponentType<T>() first.",
                nameof(componentId));
        return type;
    }

    // ── Change Detection ─────────────────────────────────────

    /// <summary>Mark a component as changed. Enables <c>HasChanged&lt;T&gt;</c> query filters (like Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    public void SetChanged<T>(EntityId entity) where T : struct, IComponent
    {
        _changedComponents ??= [];
        _changedComponents.Add((entity.Value, GetComponentId<T>()));
    }

    /// <summary>Mark a component as changed by its registered ID.</summary>
    public void SetChanged(EntityId entity, ulong componentId)
    {
        _changedComponents ??= [];
        _changedComponents.Add((entity.Value, componentId));
    }

    /// <summary>Check if any component on this entity changed this frame.</summary>
    public bool HasChanged(EntityId entity)
    {
        if (_changedComponents == null) return false;
        foreach (var (eid, _) in _changedComponents)
            if (eid == entity.Value) return true;
        return false;
    }

    /// <summary>Check if a specific component type changed on this entity (like Bevy's <c>Changed&lt;T&gt;</c>).</summary>
    public bool HasChanged<T>(EntityId entity) where T : struct, IComponent
        => _changedComponents?.Contains((entity.Value, GetComponentId<T>())) ?? false;

    /// <summary>Check if a specific component (by registered ID) changed this frame.</summary>
    public bool HasChanged(EntityId entity, ulong componentId)
        => _changedComponents?.Contains((entity.Value, componentId)) ?? false;

    /// <summary>Check if a specific component type was added to this entity this frame (like Bevy's <c>Added&lt;T&gt;</c>).</summary>
    public bool HasAdded<T>(EntityId entity) where T : struct, IComponent
        => _addedComponents?.Contains((entity.Value, GetComponentId<T>())) ?? false;

    /// <summary>Check if a specific component (by registered ID) was added this frame.</summary>
    public bool HasAdded(EntityId entity, ulong componentId)
        => _addedComponents?.Contains((entity.Value, componentId)) ?? false;

    /// <summary>Clear all change/added markers. Called automatically at the start of each Update tick.</summary>
    internal void ClearChanged()
    {
        _changedComponents?.Clear();
        _addedComponents?.Clear();
    }

    // ── Entity Info ──────────────────────────────────────────

    public ReadOnlySpan<ComponentInfo> GetType(EntityId id)
    {
        var e = _store.GetEntityById((int)id.Value);
        if (e.IsNull) return [];
        var arch = e.Archetype;
        var types = arch.ComponentTypes;
        var result = new ComponentInfo[types.Count];
        int i = 0;
        foreach (var ct in types)
            result[i++] = new ComponentInfo((ulong)ct.StructIndex, ct.StructSize);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Name(EntityId id)
    {
        var e = _store.GetEntityById((int)id.Value);
        return e.HasComponent<EntityName>() ? e.GetComponent<EntityName>().value.ToString() : string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsetName(EntityId id)
    {
        var e = _store.GetEntityById((int)id.Value);
        if (e.HasComponent<EntityName>())
            e.RemoveComponent<EntityName>();
    }

    /// <summary>
    /// Returns the entity ID if the entity is still alive, or <see langword="default"/> if it has been deleted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityId GetAlive(EntityId id)
    {
        var e = _store.GetEntityById((int)id.Value);
        return !e.IsNull ? id : default;
    }

    /// <summary>
    /// Iterate all alive entity IDs in the world (Bevy's <c>World::iter_entities()</c>).
    /// </summary>
    public ReadOnlySpan<EntityId> IterEntities()
    {
        var result = new List<EntityId>();
        foreach (var archetype in _store.Archetypes)
        {
            foreach (var entity in archetype.Entities)
            {
                if (!entity.IsNull)
                    result.Add(new EntityId((ulong)entity.Id));
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Clone an entity's components to a new entity (Bevy's <c>EntityCloner</c>).
    /// Does not clone hierarchy (children).
    /// </summary>
    public EntityId CloneEntity(EntityId source)
    {
        var src = _store.GetEntityById((int)source.Value);
        var cloned = src.CloneEntity();
        return new EntityId((ulong)cloned.Id);
    }

    /// <summary>
    /// Execute a command. If inside a <see cref="BeginDeferred"/>/<see cref="EndDeferred"/> block,
    /// the command is queued; otherwise executes immediately.
    /// </summary>
    public void Deferred(Action<KiloWorld> fn)
    {
        if (_deferredCommands != null)
            _deferredCommands.Add(fn);
        else
            fn(this);
    }

    /// <summary>Begin collecting deferred commands. All <see cref="Deferred"/> calls will be queued until <see cref="EndDeferred"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginDeferred()
    {
        _deferredCommands ??= [];
        _deferredCommands.Clear();
    }

    /// <summary>Execute all queued deferred commands and resume immediate execution mode.</summary>
    public void EndDeferred()
    {
        if (_deferredCommands == null || _deferredCommands.Count == 0) return;
        foreach (var cmd in _deferredCommands)
            cmd(this);
        _deferredCommands.Clear();
    }

    /// <summary>Create a typed entity command buffer for deferred entity operations (Bevy's <c>Commands</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloCommands Commands() => new(this);

    // ── Spawn (deferred) ─────────────────────────────────────

    /// <summary>Spawn a new entity (deferred). Returns <see cref="KiloEntityCommands"/> for chaining.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntityCommands SpawnDeferred()
    {
        EntityId id = default;
        Deferred(w => { var e = w.Entity(); id = e.Id; });
        return new KiloEntityCommands(id, this);
    }

    /// <summary>Get an <see cref="KiloEntityCommands"/> handle for deferred operations on an existing entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntityCommands EntityDeferred(EntityId id) => new(id, this);

    // ── Observer System ──────────────────────────────────────

    /// <summary>
    /// Register an observer callback for a trigger type.
    /// Inspired by Bevy's <c>app.add_observer()</c>.
    /// </summary>
    public void AddObserver<TTrigger>(Action<KiloWorld, TTrigger, EntityId> callback)
        where TTrigger : struct, IKiloTrigger
    {
        _observers ??= [];
        var type = typeof(TTrigger);
        if (!_observers.TryGetValue(type, out var list))
        {
            list = [];
            _observers[type] = list;
        }
        list.Add(callback);
    }

    /// <summary>
    /// Emit a trigger targeting an entity.
    /// <see cref="IKiloPropagatingTrigger"/> implementations automatically bubble up the entity hierarchy.
    /// </summary>
    public void EmitTrigger<TTrigger>(TTrigger trigger, EntityId entity)
        where TTrigger : struct, IKiloTrigger
    {
        _activeTriggers ??= [];
        var key = (typeof(TTrigger), entity.Value);
        if (!_activeTriggers.Add(key)) return;

        try
        {
            if (_observers != null && _observers.TryGetValue(typeof(TTrigger), out var list))
            {
                foreach (var del in list)
                    ((Action<KiloWorld, TTrigger, EntityId>)del)(this, trigger, entity);
            }

            if (trigger is IKiloPropagatingTrigger propagating && propagating.ShouldPropagate)
            {
                var e = _store.GetEntityById((int)entity.Value);
                if (!e.Parent.IsNull)
                    EmitTrigger(trigger, new EntityId((ulong)e.Parent.Id));
            }
        }
        finally
        {
            _activeTriggers.Remove(key);
        }
    }

    // ── Query ────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1> Query<T1>()
        where T1 : struct, IComponent
    {
        return new KiloQuery<T1>(_store.Query<T1>(), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2> Query<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return new KiloQuery<T1, T2>(_store.Query<T1, T2>(), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3> Query<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return new KiloQuery<T1, T2, T3>(_store.Query<T1, T2, T3>(), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4> Query<T1, T2, T3, T4>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
    {
        return new KiloQuery<T1, T2, T3, T4>(_store.Query<T1, T2, T3, T4>(), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery<T1, T2, T3, T4, T5> Query<T1, T2, T3, T4, T5>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
        where T5 : struct, IComponent
    {
        return new KiloQuery<T1, T2, T3, T4, T5>(_store.Query<T1, T2, T3, T4, T5>(), this);
    }

    // ── Cleanup ──────────────────────────────────────────────

    public void Dispose()
    {
        _resources?.Clear();
        _deferredCommands?.Clear();
        _changedComponents?.Clear();
        _addedComponents?.Clear();
        _observers?.Clear();
        _activeTriggers?.Clear();
    }
}
