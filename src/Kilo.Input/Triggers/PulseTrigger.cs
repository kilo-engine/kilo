namespace Kilo.Input.Triggers;

/// <summary>
/// Fires at regular intervals while input is held.
/// Useful for rapid-fire / auto-repeat.
/// </summary>
public struct PulseTrigger : IInputTrigger
{
    public float Interval { get; set; } = 0.1f;
    private float _elapsed;

    public PulseTrigger() { }

    public TriggerState Update(float rawMagnitude, float deltaTime)
    {
        if (rawMagnitude <= 0f)
        {
            _elapsed = 0f;
            return TriggerState.None;
        }
        _elapsed += deltaTime;
        if (_elapsed >= Interval)
        {
            _elapsed = 0f;
            return TriggerState.Triggered;
        }
        return TriggerState.Ongoing;
    }
}
