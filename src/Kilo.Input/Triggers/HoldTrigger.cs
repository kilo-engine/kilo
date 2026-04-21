namespace Kilo.Input.Triggers;

/// <summary>
/// Fires after input is held continuously for a duration.
/// Useful for charged attacks, long-press interactions.
/// </summary>
public struct HoldTrigger : IInputTrigger
{
    public float Duration { get; set; } = 0.5f;
    private float _heldTime;

    public HoldTrigger() { }

    public TriggerState Update(float rawMagnitude, float deltaTime)
    {
        if (rawMagnitude <= 0f)
        {
            _heldTime = 0f;
            return TriggerState.None;
        }
        _heldTime += deltaTime;
        return _heldTime >= Duration ? TriggerState.Triggered : TriggerState.Ongoing;
    }
}
