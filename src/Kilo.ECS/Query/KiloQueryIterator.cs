using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Iterator over query results. Wraps TinyEcs.QueryIterator with zero overhead.
/// </summary>
[SkipLocalsInit]
public ref struct KiloQueryIterator
{
    internal TinyEcs.QueryIterator _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloQueryIterator(TinyEcs.QueryIterator inner) => _inner = inner;

    /// <summary>Number of entities in the current archetype chunk.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Count;
    }

    /// <summary>Move to the next archetype chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Next() => _inner.Next();

    /// <summary>Get component data as a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TinyEcs.EntityView> Entities() => _inner.Entities();

    /// <summary>Get component data span by column index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> Data<T>(int index) where T : struct => _inner.Data<T>(index);

    /// <summary>Get column index for a component type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetColumnIndexOf<T>() where T : struct => _inner.GetColumnIndexOf<T>();

    /// <summary>Get a data row accessor for a column.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DataRow<T> GetColumn<T>(int index) where T : struct => new(_inner.GetColumn<T>(index));

    /// <summary>Get changed ticks for a column.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<uint> GetChangedTicks(int index) => _inner.GetChangedTicks(index);

    /// <summary>Get added ticks for a column.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<uint> GetAddedTicks(int index) => _inner.GetAddedTicks(index);
}
