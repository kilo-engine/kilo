using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Describes a component or tag by its ID and size. Zero-sized entries are tags.
/// Wraps TinyEcs.ComponentInfo.
/// </summary>
public readonly struct ComponentInfo
{
    internal readonly TinyEcs.ComponentInfo _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ComponentInfo(TinyEcs.ComponentInfo inner) => _inner = inner;

    /// <summary>Component ID.</summary>
    public ulong Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.ID;
    }

    /// <summary>Component data size in bytes. 0 for tags.</summary>
    public int Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Size;
    }
}
