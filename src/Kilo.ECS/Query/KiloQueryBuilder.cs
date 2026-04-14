using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Builds queries over the ECS world. Wraps TinyEcs.QueryBuilder with zero overhead.
/// </summary>
public sealed class KiloQueryBuilder
{
    internal readonly TinyEcs.QueryBuilder _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryBuilder(TinyEcs.QueryBuilder inner) => _inner = inner;

    /// <summary>Require entities to have component T.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder With<T>() where T : struct
    {
        _inner.With<T>();
        return this;
    }

    /// <summary>Require entities to have a component by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder With(ulong componentId)
    {
        _inner.With(componentId);
        return this;
    }

    /// <summary>Exclude entities that have component T.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder Without<T>() where T : struct
    {
        _inner.Without<T>();
        return this;
    }

    /// <summary>Exclude entities that have a component by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder Without(ulong componentId)
    {
        _inner.Without(componentId);
        return this;
    }

    /// <summary>Make component T optional in the query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder Optional<T>() where T : struct
    {
        _inner.Optional<T>();
        return this;
    }

    /// <summary>Make a component optional by ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryBuilder Optional(ulong componentId)
    {
        _inner.Optional(componentId);
        return this;
    }

    /// <summary>Build the query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQuery Build() => new(_inner.Build());
}
