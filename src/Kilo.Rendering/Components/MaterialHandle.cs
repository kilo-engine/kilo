namespace Kilo.Rendering;

/// <summary>
/// Strongly-typed handle to a material resource in the <see cref="RenderResourceStore"/>.
/// Prevents accidental mixing with mesh handles or raw integers.
/// </summary>
public readonly struct MaterialHandle : IEquatable<MaterialHandle>
{
    public int Value { get; }

    public MaterialHandle(int value) => Value = value;

    public bool IsValid => Value >= 0;

    public bool Equals(MaterialHandle other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MaterialHandle h && Equals(h);
    public override int GetHashCode() => Value;
    public override string ToString() => $"Material({Value})";

    public static bool operator ==(MaterialHandle left, MaterialHandle right) => left.Equals(right);
    public static bool operator !=(MaterialHandle left, MaterialHandle right) => !left.Equals(right);

    /// <summary>Represents an invalid or unassigned material handle.</summary>
    public static MaterialHandle Invalid => new(-1);
}
