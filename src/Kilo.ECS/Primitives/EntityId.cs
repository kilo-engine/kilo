using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Represents an entity identifier. Wraps a ulong value matching TinyEcs's EcsID layout.
/// Zero overhead: readonly struct with single ulong field, implicit conversions.
/// </summary>
[SkipLocalsInit]
public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
{
    public readonly ulong Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityId(ulong value) => Value = value;

    /// <summary>Whether this ID refers to a valid (non-zero) entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid() => Value != 0;

    /// <summary>Extracts the real entity ID (lower 32 bits).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong RealId() => Value & 0xFFFFFFFFul;

    /// <summary>Extracts the generation counter (upper 32 bits, lower 16).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Generation() => (int)((Value >> 32) & 0xFFFFul);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EntityId other) => Value == other.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Value.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is EntityId id && Equals(id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ulong(EntityId id) => id.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EntityId(ulong value) => new(value);

    public override string ToString() => $"EntityId({Value})";
}
