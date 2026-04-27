using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Mutable pointer to a component.
/// Inspired by Bevy's <c>Mut&lt;T&gt;</c>. Use <see cref="KiloEntity.GetPtr{T}"/> to obtain —
/// creation automatically marks the component as changed.
/// Access the component via <c>ptr.Ref</c>.
/// </summary>
[SkipLocalsInit]
public ref struct Ptr<T> where T : struct
{
    private readonly ref T _ref;
    private readonly bool _valid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Ptr(ref T value)
    {
        _ref = ref value;
        _valid = true;
    }

    /// <summary>Direct reference to the component data.</summary>
    public ref T Ref
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _ref;
    }

    /// <summary>Whether this pointer points to valid data.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsValid() => _valid;
}

/// <summary>
/// Read-only pointer to a component.
/// Inspired by Bevy's <c>Ref&lt;T&gt;</c>. Use <see cref="KiloEntity.GetPtrRO{T}"/> to obtain.
/// Does not trigger change detection.
/// </summary>
[SkipLocalsInit]
public readonly ref struct PtrRO<T> where T : struct
{
    private readonly ref readonly T _ref;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PtrRO(ref readonly T value) => _ref = ref value;

    /// <summary>Read-only reference to the component data.</summary>
    public ref readonly T Ref
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _ref;
    }
}
