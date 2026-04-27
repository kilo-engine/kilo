using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Read-only access to the current state.
/// Stored as a resource in KiloWorld.
/// Inspired by Bevy's <c>States</c> trait + <c>OnEnter</c>/<c>OnExit</c>/<c>OnTransition</c>.
/// </summary>
public sealed class State<TState> where TState : struct, Enum
{
    internal TState _current;
    internal TState _previous;
    internal bool _changed;

    // Callbacks registered via KiloApp.OnEnter / OnExit / OnTransition
    internal List<(TState state, Action<KiloWorld> system)>? _onEnter;
    internal List<(TState state, Action<KiloWorld> system)>? _onExit;
    internal List<Action<KiloWorld, TState, TState>>? _onTransition;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal State(TState initial)
    {
        _current = initial;
        _previous = initial;
        _changed = false;
    }

    /// <summary>Current state value.</summary>
    public TState Current => _current;

    /// <summary>Previous state value.</summary>
    public TState Previous => _previous;

    /// <summary>Whether the state changed this frame.</summary>
    public bool IsChanged => _changed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ApplyTransition(TState next)
    {
        _previous = _current;
        _current = next;
        _changed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearChanged() => _changed = false;
}

/// <summary>
/// Mutable access to queue state transitions.
/// Stored as a resource in KiloWorld.
/// </summary>
public sealed class NextState<TState> where TState : struct, Enum
{
    private bool _queued;
    private TState _next;

    /// <summary>Queue a state transition (applies after current frame).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(TState nextState)
    {
        _next = nextState;
        _queued = true;
    }

    /// <summary>Whether a transition is queued.</summary>
    public bool IsQueued => _queued;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TState Consume()
    {
        _queued = false;
        return _next;
    }
}
