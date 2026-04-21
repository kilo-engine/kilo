namespace Kilo.Input.Triggers;

/// <summary>
/// Fires immediately when input exceeds zero threshold.
/// The most common trigger — used for jump, interact, etc.
/// </summary>
public struct PressTrigger : IInputTrigger
{
    public TriggerState Update(float rawMagnitude, float deltaTime) =>
        rawMagnitude > 0f ? TriggerState.Triggered : TriggerState.None;
}
