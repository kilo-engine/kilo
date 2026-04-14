using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// A cached query over entities. Wraps TinyEcs.Query with zero overhead.
/// </summary>
public sealed class KiloQuery
{
    internal readonly TinyEcs.Query _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQuery(TinyEcs.Query inner) => _inner = inner;

    /// <summary>Count of matching entities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Count() => _inner.Count();

    /// <summary>Get a query iterator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator Iter() => new(_inner.Iter());

    /// <summary>Get a query iterator for a specific entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloQueryIterator Iter(EntityId entity) => new(_inner.Iter(entity.Value));
}
