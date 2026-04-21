namespace Kilo.Input.Triggers;

/// <summary>
/// Result of evaluating an input trigger.
/// Inspired by Unreal's ETriggerState.
/// </summary>
public enum TriggerState
{
    /// <summary>Input not active.</summary>
    None,
    /// <summary>Input is being processed but threshold not yet met (e.g. holding).</summary>
    Ongoing,
    /// <summary>All trigger conditions met.</summary>
    Triggered,
}
