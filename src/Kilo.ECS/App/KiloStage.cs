using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Represents an execution stage. Wraps TinyEcs.Bevy.Stage.
/// Systems are ordered within and across stages.
/// </summary>
public sealed class KiloStage
{
    internal readonly TinyEcs.Bevy.Stage _inner;

    private KiloStage(TinyEcs.Bevy.Stage inner) => _inner = inner;

    /// <summary>Stage name.</summary>
    public string Name => _inner.Name;

    /// <summary>Runs once on first frame (always single-threaded).</summary>
    public static KiloStage Startup { get; } = new(TinyEcs.Bevy.Stage.Startup);

    /// <summary>First regular update stage.</summary>
    public static KiloStage First { get; } = new(TinyEcs.Bevy.Stage.First);

    /// <summary>Before main update.</summary>
    public static KiloStage PreUpdate { get; } = new(TinyEcs.Bevy.Stage.PreUpdate);

    /// <summary>Main gameplay logic.</summary>
    public static KiloStage Update { get; } = new(TinyEcs.Bevy.Stage.Update);

    /// <summary>After main update.</summary>
    public static KiloStage PostUpdate { get; } = new(TinyEcs.Bevy.Stage.PostUpdate);

    /// <summary>Final stage (rendering, cleanup).</summary>
    public static KiloStage Last { get; } = new(TinyEcs.Bevy.Stage.Last);

    /// <summary>Create a custom stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KiloStage Custom(string name) => new(TinyEcs.Bevy.Stage.Custom(name));

    public override string ToString() => Name;
}
