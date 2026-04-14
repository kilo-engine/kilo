using System.Runtime.CompilerServices;
using TinyEcs.Bevy;

namespace Kilo.ECS;

/// <summary>
/// The main ECS container. Wraps TinyEcs.World with zero overhead.
/// All game code and plugins should use this type exclusively.
/// </summary>
public sealed class KiloWorld : IDisposable
{
    internal readonly TinyEcs.World _world;

    /// <summary>Create a new world with optional max component count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloWorld(ulong maxComponentId = 256) => _world = new(maxComponentId);

    internal KiloWorld(TinyEcs.World world) => _world = world;

    // ── Properties ───────────────────────────────────────────

    /// <summary>Maximum component entity ID.</summary>
    public ulong MaxComponentId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world.MaxComponentId;
    }

    /// <summary>Current tick for change detection.</summary>
    public uint CurrentTick
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world.CurrentTick;
    }

    /// <summary>Number of alive entities (includes component entities).</summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world.EntityCount;
    }

    /// <summary>Whether the world is in deferred mode.</summary>
    public bool IsDeferred
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world.IsDeferred;
    }

    /// <summary>Advance the tick counter for change detection.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Update() => _world.Update();

    // ── Resource Management ─────────────────────────────────────

    /// <summary>Get a resource from the world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResource<T>() where T : notnull => _world.GetResource<T>();

    /// <summary>Get a reference to a resource from the world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetResourceRef<T>() where T : notnull => ref _world.GetResourceRef<T>();

    /// <summary>Check if a resource exists.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasResource<T>() where T : notnull => _world.HasResource<T>();

    /// <summary>Add a resource to the world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddResource<T>(T resource) where T : notnull => _world.AddResource(resource);

    // ── Events ───────────────────────────────────────────────

    /// <summary>Fired when an entity is created.</summary>
    public event Action<KiloWorld, EntityId>? OnEntityCreated;

    /// <summary>Fired when an entity is deleted.</summary>
    public event Action<KiloWorld, EntityId>? OnEntityDeleted;

    /// <summary>Fired when a component is set on an entity.</summary>
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentSet;

    /// <summary>Fired when a component is removed from an entity.</summary>
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentUnset;

    /// <summary>Fired when a component is added for the first time.</summary>
    public event Action<KiloWorld, EntityId, ComponentInfo>? OnComponentAdded;

    private bool _eventsWired;

    internal void WireEvents()
    {
        if (_eventsWired) return;
        _eventsWired = true;

        _world.OnEntityCreated += (w, id) => OnEntityCreated?.Invoke(this, new EntityId(id));
        _world.OnEntityDeleted += (w, id) => OnEntityDeleted?.Invoke(this, new EntityId(id));
        _world.OnComponentSet += (w, id, ci) => OnComponentSet?.Invoke(this, new EntityId(id), new ComponentInfo(ci));
        _world.OnComponentUnset += (w, id, ci) => OnComponentUnset?.Invoke(this, new EntityId(id), new ComponentInfo(ci));
        _world.OnComponentAdded += (w, id, ci) => OnComponentAdded?.Invoke(this, new EntityId(id), new ComponentInfo(ci));
    }

    // ── Entity Creation ──────────────────────────────────────

    /// <summary>Create or get an entity. Use id=0 to spawn a new entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity(ulong id = 0) => new(_world.Entity(id), this);

    /// <summary>Get or create an entity from a component type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity<T>() where T : struct => new(_world.Entity<T>(), this);

    /// <summary>Create or get a named entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Entity(string name) => new(_world.Entity(name), this);

    // ── Entity Lifecycle ─────────────────────────────────────

    /// <summary>Delete an entity and its children.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete(EntityId entity) => _world.Delete(entity.Value);

    /// <summary>Check if an entity is alive.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Exists(EntityId entity) => _world.Exists(entity.Value);

    // ── Component Operations (generic) ───────────────────────

    /// <summary>Set a component on an entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(EntityId entity, T component = default) where T : struct => _world.Set(entity.Value, component);

    /// <summary>Remove a component or tag from an entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unset<T>(EntityId entity) where T : struct => _world.Unset<T>(entity.Value);

    /// <summary>Check if an entity has a component or tag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>(EntityId entity) where T : struct => _world.Has<T>(entity.Value);

    /// <summary>Get a reference to a component on an entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>(EntityId entity) where T : struct => ref _world.Get<T>(entity.Value);

    // ── Component Operations (by ID) ─────────────────────────

    /// <summary>Remove a component by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unset(EntityId entity, ulong componentId) => _world.Unset(entity.Value, componentId);

    /// <summary>Check if entity has a component by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(EntityId entity, ulong componentId) => _world.Has(entity.Value, componentId);

    // ── Change Detection ─────────────────────────────────────

    /// <summary>Mark a component as changed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChanged<T>(EntityId entity) where T : struct => _world.SetChanged<T>(entity.Value);

    /// <summary>Mark a component as changed by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChanged(EntityId entity, ulong componentId) => _world.SetChanged(entity.Value, componentId);

    // ── Entity Info ──────────────────────────────────────────

    /// <summary>Get the archetype signature of an entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<ComponentInfo> GetType(EntityId id)
    {
        var span = _world.GetType(id.Value);
        // Convert TinyEcs.ComponentInfo span to Kilo ComponentInfo span
        // Since ComponentInfo is a readonly struct wrapping the inner type,
        // we need to create a new array. This is only used for inspection, not hot paths.
        if (span.IsEmpty) return [];
        var result = new ComponentInfo[span.Length];
        for (int i = 0; i < span.Length; i++)
            result[i] = new ComponentInfo(span[i]);
        return result;
    }

    /// <summary>Get the entity name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Name(EntityId id) => _world.Name(id.Value);

    /// <summary>Remove the entity name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsetName(EntityId id) => _world.UnsetName(id.Value);

    /// <summary>Resolve an entity ID to its alive version.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityId GetAlive(EntityId id) => new(_world.GetAlive(id.Value));

    // ── Deferred Operations ──────────────────────────────────

    /// <summary>Execute a block of deferred operations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deferred(Action<KiloWorld> fn)
    {
        _world.Deferred(w => fn(this));
    }

    /// <summary>Begin deferred mode.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginDeferred() => _world.BeginDeferred();

    /// <summary>End deferred mode and merge operations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndDeferred() => _world.EndDeferred();

    // ── Query ────────────────────────────────────────────────

    /// <summary>Create a new query builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder QueryBuilder() => new(_world.QueryBuilder());

    // ── Cleanup ──────────────────────────────────────────────

    /// <summary>Remove empty archetypes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveEmptyArchetypes() => _world.RemoveEmptyArchetypes();

    /// <summary>Dispose the world and release resources.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _world.Dispose();
}
