namespace Kilo.Input.Triggers;

/// <summary>
/// Determines when an input action fires.
/// Implementations define activation patterns like press, hold, pulse.
/// Inspired by Unreal's UInputTrigger.
/// </summary>
public interface IInputTrigger
{
    /// <param name="rawMagnitude">Normalized input strength [0..1].</param>
    /// <param name="deltaTime">Frame delta in seconds.</param>
    TriggerState Update(float rawMagnitude, float deltaTime);
}
