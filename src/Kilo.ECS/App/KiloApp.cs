using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Application framework for Kilo ECS.
/// Uses a manual stage system with ordering support.
/// </summary>
public class KiloApp
{
    private KiloWorld? _world;
    private readonly List<Action<KiloWorld>> _startupSystems = new();
    private readonly Dictionary<string, List<Action<KiloWorld>>> _stageSystems = [];
    internal Dictionary<string, List<Action<KiloWorld>>> StageSystems => _stageSystems;
    private readonly List<string> _stageOrder = new() { "First", "PreUpdate", "Update", "PostUpdate", "Last" };
    private readonly List<Action<KiloWorld>> _stateTransitions = new();
    private bool _startupRun;
    private readonly ThreadingMode _threadingMode;
    private readonly Dictionary<string, Func<KiloWorld, bool>> _systemSetConditions = [];
    private readonly List<Action> _clearStateActions = [];

    public KiloApp(ThreadingMode threadingMode = ThreadingMode.Auto)
    {
        _threadingMode = threadingMode;
    }

    public KiloApp(KiloWorld world, ThreadingMode threadingMode = ThreadingMode.Auto)
    {
        _world = world;
        _threadingMode = threadingMode;
    }

    /// <summary>Threading mode for system execution.</summary>
    public ThreadingMode ThreadingMode => _threadingMode;

    public KiloWorld World
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world ??= new KiloWorld();
    }

    // ── Resources & State ─────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddResource<T>(T resource) where T : notnull
    {
        World.AddResource(resource);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddState<TState>(TState initialState) where TState : struct, Enum
    {
        World.AddResource(new State<TState>(initialState));
        World.AddResource(new NextState<TState>());
        TrackStateChanged<TState>();
        _stateTransitions.Add(w =>
        {
            var next = w.GetResource<NextState<TState>>();
            if (!next.IsQueued) return;
            var state = w.GetResource<State<TState>>();
            var from = state._current;
            state.ApplyTransition(next.Consume());
            var to = state._current;
            if (state._onExit != null)
                foreach (var (s, sys) in state._onExit)
                    if (EqualityComparer<TState>.Default.Equals(s, from)) sys(w);
            if (state._onEnter != null)
                foreach (var (s, sys) in state._onEnter)
                    if (EqualityComparer<TState>.Default.Equals(s, to)) sys(w);
            if (state._onTransition != null)
                foreach (var sys in state._onTransition)
                    sys(w, from, to);
        });
        return this;
    }

    /// <summary>Register a system to run when state enters a specific value (Bevy's <c>OnEnter</c>).</summary>
    public KiloApp OnEnter<TState>(TState state, Action<KiloWorld> system) where TState : struct, Enum
    {
        var s = World.GetResource<State<TState>>();
        s._onEnter ??= [];
        s._onEnter.Add((state, system));
        return this;
    }

    /// <summary>Register a system to run when state exits a specific value (Bevy's <c>OnExit</c>).</summary>
    public KiloApp OnExit<TState>(TState state, Action<KiloWorld> system) where TState : struct, Enum
    {
        var s = World.GetResource<State<TState>>();
        s._onExit ??= [];
        s._onExit.Add((state, system));
        return this;
    }

    /// <summary>Register a system to run on any state transition (Bevy's <c>OnTransition</c>).</summary>
    public KiloApp OnTransition<TState>(Action<KiloWorld, TState, TState> system) where TState : struct, Enum
    {
        var s = World.GetResource<State<TState>>();
        s._onTransition ??= [];
        s._onTransition.Add(system);
        return this;
    }

    // ── Plugin ────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddPlugin(IKiloPlugin plugin)
    {
        plugin.Build(this);
        return this;
    }

    // ── Stage Management ──────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloStageConfigurator AddStage(KiloStage stage)
    {
        if (!_stageOrder.Contains(stage.Name))
            _stageOrder.Add(stage.Name);
        return new KiloStageConfigurator(stage, this);
    }

    // ── Execution ─────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RunStartup()
    {
        foreach (var sys in _startupSystems)
            sys(World);
        _startupRun = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Run()
    {
        if (!_startupRun) RunStartup();
        Update();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update()
    {
        foreach (var stageName in _stageOrder)
        {
            if (_stageSystems.TryGetValue(stageName, out var systems))
            {
                foreach (var system in systems)
                    system(World);
            }
        }

        // Apply queued state transitions (fires OnEnter/OnExit/OnTransition)
        foreach (var transition in _stateTransitions)
            transition(World);

        // Advance world tick (clears change/added tracking)
        World.Update();

        // Clear state changed flags
        ClearAllStateChanged();
    }

    /// <summary>Run a system once immediately (Bevy's <c>world::run_system_once()</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RunSystemOnce(Action<KiloWorld> system) => system(World);

    // ── System Registration ───────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem(KiloStage stage, Action<KiloWorld> system)
    {
        if (stage == KiloStage.Startup) { _startupSystems.Add(system); }
        else
        {
            if (!_stageSystems.TryGetValue(stage.Name, out var list))
            {
                list = [];
                _stageSystems[stage.Name] = list;
            }
            list.Add(system);
        }
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem(KiloStage stage, Action system)
    {
        if (stage == KiloStage.Startup) _startupSystems.Add(_ => system());
        else AddSystem(stage, _ => system());
        return this;
    }

    /// <summary>Add a system with a run condition (Bevy's <c>.run_if()</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem(KiloStage stage, Action<KiloWorld> system, Func<KiloWorld, bool> condition)
        => AddSystem(stage, w => { if (condition(w)) system(w); });

    /// <summary>Only run a system when a resource exists (Bevy's <c>resource_exists()</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem<TResource>(KiloStage stage, Action<KiloWorld> system) where TResource : notnull
        => AddSystem(stage, system, w => w.HasResource<TResource>());

    /// <summary>Only run a system when state equals a specific value (Bevy's <c>state_equals()</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KiloApp AddSystem<TState>(KiloStage stage, TState requiredState, Action<KiloWorld> system) where TState : struct, Enum
        => AddSystem(stage, system, w =>
        {
            if (!w.HasResource<State<TState>>()) return false;
            return EqualityComparer<TState>.Default.Equals(w.GetResource<State<TState>>()._current, requiredState);
        });

    /// <summary>Add a system with fluent run-condition chaining.</summary>
    public KiloSystemBuilder AddSystemIf(KiloStage stage, Action<KiloWorld> system)
    {
        AddSystem(stage, system);
        return new KiloSystemBuilder(stage, this);
    }

    // ── System Sets (Bevy's SystemSet) ────────────────────────

    /// <summary>Add a system to a named set. Systems in the same set share run conditions from <see cref="ConfigureSet"/>.</summary>
    public KiloApp AddSystemToSet(string setName, KiloStage stage, Action<KiloWorld> system)
    {
        AddSystem(stage, w =>
        {
            if (_systemSetConditions.TryGetValue(setName, out var condition) && !condition(w))
                return;
            system(w);
        });
        return this;
    }

    /// <summary>Configure a run condition for all systems in a set (Bevy's <c>.configure_sets()</c>).</summary>
    public KiloApp ConfigureSet(string setName, Func<KiloWorld, bool> condition)
    {
        _systemSetConditions[setName] = condition;
        return this;
    }

    /// <summary>Configure a set to only run when a state equals a specific value.</summary>
    public KiloApp ConfigureSet<TState>(string setName, TState required) where TState : struct, Enum
        => ConfigureSet(setName, w =>
        {
            if (!w.HasResource<State<TState>>()) return false;
            return EqualityComparer<TState>.Default.Equals(w.GetResource<State<TState>>()._current, required);
        });

    // ── Internal ──────────────────────────────────────────────

    internal void InsertBefore(KiloStage stage, KiloStage target)
    {
        _stageOrder.Remove(stage.Name);
        var idx = _stageOrder.IndexOf(target.Name);
        if (idx >= 0) _stageOrder.Insert(idx, stage.Name);
    }

    internal void InsertAfter(KiloStage stage, KiloStage target)
    {
        _stageOrder.Remove(stage.Name);
        var idx = _stageOrder.IndexOf(target.Name);
        if (idx >= 0) _stageOrder.Insert(idx + 1, stage.Name);
    }

    internal void TrackStateChanged<TState>() where TState : struct, Enum
    {
        _clearStateActions.Add(() =>
        {
            if (World.HasResource<State<TState>>())
                World.GetResource<State<TState>>().ClearChanged();
        });
    }

    private void ClearAllStateChanged()
    {
        foreach (var action in _clearStateActions)
            action();
    }
}

public sealed class KiloStageConfigurator
{
    private readonly KiloStage _stage;
    private readonly KiloApp _parent;

    internal KiloStageConfigurator(KiloStage stage, KiloApp parent) { _stage = stage; _parent = parent; }

    /// <summary>This stage runs before another stage.</summary>
    public KiloStageConfigurator Before(KiloStage target) { _parent.InsertBefore(_stage, target); return this; }

    /// <summary>This stage runs after another stage.</summary>
    public KiloStageConfigurator After(KiloStage target) { _parent.InsertAfter(_stage, target); return this; }

    /// <summary>Finalize stage configuration.</summary>
    public KiloApp Build() => _parent;
}

/// <summary>Fluent configurator for adding run conditions to a system (Bevy's <c>.run_if()</c>).</summary>
public sealed class KiloSystemBuilder
{
    private readonly KiloStage _stage;
    private readonly KiloApp _app;

    internal KiloSystemBuilder(KiloStage stage, KiloApp app) { _stage = stage; _app = app; }

    /// <summary>Run only when the given condition returns true.</summary>
    public KiloApp RunIf(Func<KiloWorld, bool> condition)
    {
        var systems = _app.StageSystems[_stage.Name];
        var idx = systems.Count - 1;
        var original = systems[idx];
        systems[idx] = w => { if (condition(w)) original(w); };
        return _app;
    }

    /// <summary>Run only when a resource of type <typeparamref name="T"/> exists.</summary>
    public KiloApp RunIfResource<T>() where T : notnull => RunIf(w => w.HasResource<T>());

    /// <summary>Run only when a state equals a specific value.</summary>
    public KiloApp RunIfState<TState>(TState required) where TState : struct, Enum
        => RunIf(w => EqualityComparer<TState>.Default.Equals(w.GetResource<State<TState>>()._current, required));
}
