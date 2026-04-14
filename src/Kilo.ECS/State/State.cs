using System.Runtime.CompilerServices;

namespace Kilo.ECS;

/// <summary>
/// Read-only access to the current state. Wraps TinyEcs.Bevy.State&lt;TState&gt;.
/// </summary>
public sealed class State<TState> where TState : struct, Enum
{
    internal readonly TinyEcs.Bevy.State<TState> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal State(TinyEcs.Bevy.State<TState> inner) => _inner = inner;

    /// <summary>Current state value.</summary>
    public TState Current => _inner.Current;

    /// <summary>Previous state value.</summary>
    public TState Previous => _inner.Previous;

    /// <summary>Whether the state changed this frame.</summary>
    public bool IsChanged => _inner.IsChanged;
}

/// <summary>
/// Mutable access to queue state transitions. Wraps TinyEcs.Bevy.NextState&lt;TState&gt;.
/// </summary>
public sealed class NextState<TState> where TState : struct, Enum
{
    internal readonly TinyEcs.Bevy.NextState<TState> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NextState(TinyEcs.Bevy.NextState<TState> inner) => _inner = inner;

    /// <summary>Queue a state transition (applies after current frame).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(TState nextState) => _inner.Set(nextState);

    /// <summary>Whether a transition is queued.</summary>
    public bool IsQueued => _inner.IsQueued;
}
