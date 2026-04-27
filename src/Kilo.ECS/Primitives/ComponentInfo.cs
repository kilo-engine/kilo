using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Describes a component or tag by its ID and size. Zero-sized entries are tags.
/// </summary>
public readonly struct ComponentInfo : IEquatable<ComponentInfo>
{
    /// <summary>Component type ID.</summary>
    public readonly ulong Id;

    /// <summary>Component data size in bytes. 0 for tags.</summary>
    public readonly int Size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentInfo(ulong id, int size)
    {
        Id = id;
        Size = size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ComponentInfo other) => Id == other.Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is ComponentInfo ci && Equals(ci);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ComponentInfo a, ComponentInfo b) => a.Id == b.Id;
    public static bool operator !=(ComponentInfo a, ComponentInfo b) => a.Id != b.Id;

    public override string ToString() => Size == 0 ? $"Tag({Id})" : $"Component({Id}, {Size}B)";
}
