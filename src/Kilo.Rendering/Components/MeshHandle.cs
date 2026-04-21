namespace Kilo.Rendering;

/// <summary>
/// Strongly-typed handle to a mesh resource in the <see cref="RenderResourceStore"/>.
/// Prevents accidental mixing with material handles or raw integers.
/// </summary>
public readonly struct MeshHandle : IEquatable<MeshHandle>
{
    public int Value { get; }

    public MeshHandle(int value) => Value = value;

    public bool IsValid => Value >= 0;

    public bool Equals(MeshHandle other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MeshHandle h && Equals(h);
    public override int GetHashCode() => Value;
    public override string ToString() => $"Mesh({Value})";

    public static bool operator ==(MeshHandle left, MeshHandle right) => left.Equals(right);
    public static bool operator !=(MeshHandle left, MeshHandle right) => !left.Equals(right);

    /// <summary>Represents an invalid or unassigned mesh handle.</summary>
    public static MeshHandle Invalid => new(-1);
}
