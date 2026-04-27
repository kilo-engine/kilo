using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;

namespace Kilo.ECS;

/// <summary>
/// Typed command buffer for deferred entity operations.
/// Inspired by Bevy's <c>Commands</c>.
/// Create via <see cref="KiloWorld.Commands()"/>.
/// </summary>
public readonly struct KiloCommands
{
    private readonly KiloWorld _world;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloCommands(KiloWorld world) => _world = world;

    /// <summary>Spawn a new entity and return a command handle for chaining (Bevy's <c>commands.spawn()</c>).</summary>
    public KiloEntityCommands Spawn()
    {
        // Use a box to capture the spawned entity ID across deferred closures
        var idBox = new EntityId[1];
        _world.Deferred(w => { idBox[0] = w.Entity().Id; });
        return new KiloEntityCommands(idBox, _world);
    }

    /// <summary>Get a command handle for an existing entity (Bevy's <c>commands.entity(id)</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntityCommands Entity(EntityId id) => new(id, _world);
}

/// <summary>
/// Typed command handle for deferred entity operations.
/// Inspired by Bevy's <c>EntityCommands</c>.
/// Supports chaining: <c>cmd.Insert(comp).Remove&lt;T&gt;().Despawn()</c>.
/// </summary>
public readonly struct KiloEntityCommands
{
    private readonly EntityId _id;
    private readonly EntityId[]? _idBox; // for spawn: resolved when deferred executes
    private readonly KiloWorld _world;

    /// <summary>Existing entity constructor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloEntityCommands(EntityId id, KiloWorld world) { _id = id; _idBox = null; _world = world; }

    /// <summary>Spawn constructor — ID resolved lazily via box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloEntityCommands(EntityId[] idBox, KiloWorld world) { _id = default; _idBox = idBox; _world = world; }

    /// <summary>The entity ID this command targets.</summary>
    public EntityId Id => _idBox != null ? _idBox[0] : _id;

    /// <summary>Insert a component onto the entity (deferred). Bevy's <c>commands.entity(id).insert()</c>.</summary>
    public KiloEntityCommands Insert<T>(T component) where T : struct, IComponent
    {
        var id = Id; // capture for non-spawn case
        if (_idBox != null)
        {
            // Spawn case: ID resolved later, must capture the box
            var box = _idBox;
            _world.Deferred(w => w.Set(box[0], component));
        }
        else
        {
            _world.Deferred(w => w.Set(id, component));
        }
        return this;
    }

    /// <summary>Remove a component from the entity (deferred). Bevy's <c>commands.entity(id).remove()</c>.</summary>
    public KiloEntityCommands Remove<T>() where T : struct, IComponent
    {
        var box = _idBox;
        var id = _id;
        _world.Deferred(w => w.Unset<T>(box != null ? box[0] : id));
        return this;
    }

    /// <summary>Despawn the entity (deferred). Bevy's <c>commands.entity(id).despawn()</c>.</summary>
    public void Despawn()
    {
        var box = _idBox;
        var id = _id;
        _world.Deferred(w => w.Delete(box != null ? box[0] : id));
    }

    /// <summary>Add a child entity (deferred).</summary>
    public KiloEntityCommands AddChild(EntityId child)
    {
        var box = _idBox;
        var id = _id;
        _world.Deferred(w =>
        {
            var parent = w.Entity(box != null ? box[0] : id);
            parent.AddChild(w.Entity(child));
        });
        return this;
    }

    /// <summary>Execute an arbitrary action on the entity (deferred).</summary>
    public KiloEntityCommands With(Action<KiloWorld, EntityId> action)
    {
        var box = _idBox;
        var id = _id;
        _world.Deferred(w => action(w, box != null ? box[0] : id));
        return this;
    }
}
