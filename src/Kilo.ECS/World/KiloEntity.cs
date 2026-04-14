using System.Runtime.CompilerServices;
using TinyEcs;

namespace Kilo.ECS;

/// <summary>
/// Lightweight handle to an entity. Wraps TinyEcs.EntityView with zero overhead.
/// All operations are forwarded via AggressiveInlining.
/// </summary>
[SkipLocalsInit]
#pragma warning disable CS0660 // ref structs can't override Equals(object)
public readonly ref struct KiloEntity
{
    internal readonly TinyEcs.EntityView _inner;
    internal readonly KiloWorld? _kiloWorld;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloEntity(TinyEcs.EntityView inner, KiloWorld kiloWorld)
    {
        _inner = inner;
        _kiloWorld = kiloWorld;
    }

    /// <summary>The entity's unique identifier.</summary>
    public EntityId Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_inner.ID);
    }

    /// <summary>The entity's generation counter.</summary>
    public int Generation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Generation;
    }

    /// <summary>A shared invalid entity handle.</summary>
    public static KiloEntity Invalid => default;

    // ── Component Operations ─────────────────────────────────

    /// <summary>Set a component on this entity. Returns self for chaining.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Set<T>(T component = default) where T : struct
    {
        _inner.Set(component);
        return this;
    }

    /// <summary>Remove a component or tag from this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Unset<T>() where T : struct
    {
        _inner.Unset<T>();
        return this;
    }

    /// <summary>Remove a component or tag by ID from this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Unset(ulong componentId)
    {
        _inner.Unset(componentId);
        return this;
    }

    /// <summary>Remove a component or tag from this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Unset(EntityId componentId)
    {
        _inner.Unset(componentId.Value);
        return this;
    }

    /// <summary>Get a reference to a component on this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T Get<T>() where T : struct => ref _inner.Get<T>();

    /// <summary>Check if this entity has a component or tag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Has<T>() where T : struct => _inner.Has<T>();

    /// <summary>Check if this entity has a component or tag by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Has(ulong componentId) => _inner.Has(componentId);

    /// <summary>Check if this entity has a component or tag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Has(EntityId componentId) => _inner.Has(componentId.Value);

    // ── Hierarchy ────────────────────────────────────────────

    /// <summary>Add a child entity. Uses TinyEcs built-in Parent/ChildOf relation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity AddChild(KiloEntity child)
    {
        _inner.AddChild(child._inner.ID);
        return this;
    }

    /// <summary>Remove a child entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity RemoveChild(KiloEntity child)
    {
        _inner.RemoveChild(child._inner.ID);
        return this;
    }

    // ── Entity Lifecycle ─────────────────────────────────────

    /// <summary>Delete this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Delete() => _inner.Delete();

    /// <summary>Check if this entity is alive.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Exists() => _inner.Exists();

    /// <summary>Mark a component as changed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void SetChanged<T>() where T : struct => _inner.SetChanged<T>();

    /// <summary>Get the archetype signature of this entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<TinyEcs.ComponentInfo> Type() => _inner.Type();

    // ── Equality ─────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(EntityId other) => _inner.ID == other.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(KiloEntity other) => _inner.ID == other._inner.ID;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => _inner.GetHashCode();

    // Note: ref structs cannot override Equals(object). Use Equals(KiloEntity) instead.

    public static bool operator ==(KiloEntity a, KiloEntity b) => a._inner.ID == b._inner.ID;
    public static bool operator !=(KiloEntity a, KiloEntity b) => a._inner.ID != b._inner.ID;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EntityId(KiloEntity e) => new(e._inner.ID);
}
