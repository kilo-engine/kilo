using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Mutable pointer to a component. Wraps TinyEcs.Ptr&lt;T&gt; with zero overhead.
/// Access the component via <c>ptr.Ref</c>.
/// </summary>
[SkipLocalsInit]
public ref struct Ptr<T> where T : struct
{
    internal TinyEcs.Ptr<T> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Ptr(TinyEcs.Ptr<T> inner) => _inner = inner;

    /// <summary>Direct reference to the component data.</summary>
    public ref T Ref
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _inner.Ref;
    }

    /// <summary>Whether this pointer points to valid data.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsValid() => _inner.IsValid();
}

/// <summary>
/// Read-only pointer to a component. Wraps TinyEcs.PtrRO&lt;T&gt; with zero overhead.
/// </summary>
[SkipLocalsInit]
public readonly ref struct PtrRO<T> where T : struct
{
    internal readonly TinyEcs.PtrRO<T> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PtrRO(TinyEcs.PtrRO<T> inner) => _inner = inner;

    /// <summary>Read-only reference to the component data.</summary>
    public ref readonly T Ref
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _inner.Ref;
    }
}

/// <summary>
/// Row accessor for iterating component data in a query. Wraps TinyEcs.DataRow&lt;T&gt;.
/// </summary>
[SkipLocalsInit]
public ref struct DataRow<T> where T : struct
{
    internal TinyEcs.DataRow<T> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DataRow(TinyEcs.DataRow<T> inner) => _inner = inner;

    /// <summary>Pointer to the current component value.</summary>
    public Ptr<T> Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_inner.Value);
    }

    /// <summary>Size of each element in bytes.</summary>
    public nint Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Size;
    }

    /// <summary>Advance to the next element.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Next() => _inner.Next();
}
