using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Represents an execution stage. Backend-agnostic, backed by string name.
/// </summary>
public readonly struct KiloStage : IEquatable<KiloStage>
{
    public readonly string Name;

    private KiloStage(string name) => Name = name;

    /// <summary>Runs once on first frame.</summary>
    public static KiloStage Startup => new("Startup");

    /// <summary>First regular update stage.</summary>
    public static KiloStage First => new("First");

    /// <summary>Before main update.</summary>
    public static KiloStage PreUpdate => new("PreUpdate");

    /// <summary>Main gameplay logic.</summary>
    public static KiloStage Update => new("Update");

    /// <summary>After main update.</summary>
    public static KiloStage PostUpdate => new("PostUpdate");

    /// <summary>Final stage (rendering, cleanup).</summary>
    public static KiloStage Last => new("Last");

    /// <summary>Create a custom stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KiloStage Custom(string name) => new(name);

    // ── Equality ────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(KiloStage other) => Name == other.Name;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is KiloStage s && Equals(s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Name.GetHashCode();

    public static bool operator ==(KiloStage a, KiloStage b) => a.Name == b.Name;
    public static bool operator !=(KiloStage a, KiloStage b) => a.Name != b.Name;

    public override string ToString() => Name;
}
