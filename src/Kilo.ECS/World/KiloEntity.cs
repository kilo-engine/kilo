using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;

namespace Kilo.ECS;

/// <summary>
/// Lightweight handle to an entity with zero overhead.
/// </summary>
[SkipLocalsInit]
#pragma warning disable CS0660
public readonly ref struct KiloEntity
{
    internal readonly Entity _inner;
    internal readonly KiloWorld? _kiloWorld;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloEntity(Entity inner, KiloWorld kiloWorld)
    {
        _inner = inner;
        _kiloWorld = kiloWorld;
    }

    public EntityId Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new((ulong)_inner.Id);
    }

    public static KiloEntity Invalid => default;

    // ── Component Operations ─────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Set<T>(T component = default) where T : struct, IComponent
    {
        _inner.AddComponent(component);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity Unset<T>() where T : struct, IComponent
    {
        _inner.RemoveComponent<T>();
        return this;
    }

    /// <summary>Remove a component by registered ID. Requires KiloWorld.RegisterComponentType{T}().</summary>
    public KiloEntity Unset(ulong componentId) { _kiloWorld?.Unset(Id, componentId); return this; }
    /// <summary>Remove a component by registered ID. Requires KiloWorld.RegisterComponentType{T}().</summary>
    public KiloEntity Unset(EntityId componentId) { _kiloWorld?.Unset(Id, componentId.Value); return this; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T Get<T>() where T : struct, IComponent => ref _inner.GetComponent<T>();

    /// <summary>Get a mutable pointer to component T. Marks the component as changed (like Bevy's <c>Mut&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ptr<T> GetPtr<T>() where T : struct, IComponent
    {
        _kiloWorld?.SetChanged<T>(Id);
        return new Ptr<T>(ref _inner.GetComponent<T>());
    }

    /// <summary>Get a read-only pointer. Does not trigger change detection (like Bevy's <c>Ref&lt;T&gt;</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PtrRO<T> GetPtrRO<T>() where T : struct, IComponent
        => new(ref _inner.GetComponent<T>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Has<T>() where T : struct, IComponent => _inner.HasComponent<T>();

    /// <summary>Check if entity has a component by registered ID. Requires KiloWorld.RegisterComponentType{T}().</summary>
    public readonly bool Has(ulong componentId) => _kiloWorld?.Has(Id, componentId) ?? false;
    /// <summary>Check if entity has a component by registered ID. Requires KiloWorld.RegisterComponentType{T}().</summary>
    public readonly bool Has(EntityId componentId) => _kiloWorld?.Has(Id, componentId.Value) ?? false;

    // ── Hierarchy ────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity AddChild(KiloEntity child)
    {
        _inner.AddChild(child._inner);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloEntity RemoveChild(KiloEntity child)
    {
        _inner.RemoveChild(child._inner);
        return this;
    }

    // ── Entity Lifecycle ─────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Delete() => _inner.DeleteEntity();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Exists() => !_inner.IsNull;

    /// <summary>Mark a component as changed. Powers <c>HasChanged&lt;T&gt;</c> query filters.</summary>
    public readonly void SetChanged<T>() where T : struct, IComponent => _kiloWorld?.SetChanged<T>(Id);

    public readonly ReadOnlySpan<ComponentInfo> Type()
    {
        if (_inner.IsNull) return [];
        var arch = _inner.Archetype;
        var types = arch.ComponentTypes;
        var result = new ComponentInfo[types.Count];
        int i = 0;
        foreach (var ct in types)
            result[i++] = new ComponentInfo((ulong)ct.StructIndex, ct.StructSize);
        return result;
    }

    // ── Equality ─────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(EntityId other) => _inner.Id == (int)other.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(KiloEntity other) => _inner.Id == other._inner.Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => _inner.Id.GetHashCode();

    public static bool operator ==(KiloEntity a, KiloEntity b) => a._inner.Id == b._inner.Id;
    public static bool operator !=(KiloEntity a, KiloEntity b) => a._inner.Id != b._inner.Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EntityId(KiloEntity e) => new((ulong)e._inner.Id);
}
