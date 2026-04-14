using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// The main application framework. Wraps TinyEcs.Bevy.App.
/// Follows Bevy's philosophy: everything except ECS is a plugin.
/// </summary>
public class KiloApp
{
    internal readonly TinyEcs.Bevy.App _app;
    private KiloWorld? _world;

    /// <summary>Create a new app with a fresh world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp(ThreadingMode threadingMode = ThreadingMode.Auto)
    {
        var mode = threadingMode switch
        {
            ThreadingMode.Single => TinyEcs.Bevy.ThreadingMode.Single,
            ThreadingMode.Multi => TinyEcs.Bevy.ThreadingMode.Multi,
            _ => TinyEcs.Bevy.ThreadingMode.Auto,
        };
        _app = new TinyEcs.Bevy.App(mode);
    }

    /// <summary>Create an app wrapping an existing world.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp(KiloWorld world, ThreadingMode threadingMode = ThreadingMode.Auto)
    {
        var mode = threadingMode switch
        {
            ThreadingMode.Single => TinyEcs.Bevy.ThreadingMode.Single,
            ThreadingMode.Multi => TinyEcs.Bevy.ThreadingMode.Multi,
            _ => TinyEcs.Bevy.ThreadingMode.Auto,
        };
        _app = new TinyEcs.Bevy.App(world._world, mode);
        _world = world;
    }

    /// <summary>Access the underlying KiloWorld.</summary>
    public KiloWorld World
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world ??= new KiloWorld(_app.GetWorld());
    }

    // ── Resource Management ──────────────────────────────────

    /// <summary>Add a global resource.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddResource<T>(T resource) where T : notnull
    {
        _app.AddResource(resource);
        return this;
    }

    /// <summary>Add a state machine.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddState<TState>(TState initialState) where TState : struct, Enum
    {
        _app.AddState(initialState);
        return this;
    }

    // ── Plugin System ────────────────────────────────────────

    /// <summary>Register a plugin.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddPlugin(IKiloPlugin plugin)
    {
        plugin.Build(this);
        return this;
    }

    // ── Stage Management ─────────────────────────────────────

    /// <summary>Add a custom stage with ordering.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloStageConfigurator AddStage(KiloStage stage)
    {
        return new KiloStageConfigurator(_app.AddStage(stage._inner), this);
    }

    // ── Execution ────────────────────────────────────────────

    /// <summary>Run startup systems (once).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RunStartup() => _app.RunStartup();

    /// <summary>Run all stages (startup runs automatically on first call).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Run() => _app.Run();

    /// <summary>Run a single update tick.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update() => _app.Update();

    // ── Simple System Registration ───────────────────────────

    /// <summary>Add a system that receives the KiloWorld.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem(KiloStage stage, Action<KiloWorld> system)
    {
        _app.AddSystem(stage._inner, new TinyEcs.Bevy.FunctionalSystem(w => system(World)));
        return this;
    }

    /// <summary>Add a system with no parameters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem(KiloStage stage, Action system)
    {
        _app.AddSystem(stage._inner, new TinyEcs.Bevy.FunctionalSystem(_ => system()));
        return this;
    }
}

/// <summary>
/// Configures stage ordering.
/// </summary>
public sealed class KiloStageConfigurator
{
    private readonly TinyEcs.Bevy.StageConfigurator _inner;
    private readonly KiloApp _parent;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal KiloStageConfigurator(TinyEcs.Bevy.StageConfigurator inner, KiloApp parent)
    {
        _inner = inner;
        _parent = parent;
    }

    /// <summary>This stage runs before another stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloStageConfigurator Before(KiloStage stage)
    {
        _inner.Before(stage._inner);
        return this;
    }

    /// <summary>This stage runs after another stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloStageConfigurator After(KiloStage stage)
    {
        _inner.After(stage._inner);
        return this;
    }

    /// <summary>Finalize stage configuration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp Build()
    {
        _inner.Build();
        return _parent;
    }
}
