using System.Numerics;

namespace Kilo.Input.Processing;

/// <summary>
/// Dead zone processing with radial dead zone + linear remapping.
/// Inspired by Bevy's AxisSettings and Unreal's UInputModifierDeadZone.
/// </summary>
public static class DeadZone
{
    /// <summary>
    /// Radial dead zone for 2D sticks.
    /// Values within deadZone are clamped to zero.
    /// Values outside are linearly remapped to fill [0, 1].
    /// </summary>
    public static Vector2 ApplyRadial(float x, float y, float deadZone)
    {
        float magnitude = MathF.Sqrt(x * x + y * y);
        if (magnitude <= deadZone) return Vector2.Zero;

        float remapped = (magnitude - deadZone) / (1.0f - deadZone);
        float scale = remapped / magnitude;
        return new Vector2(x * scale, y * scale);
    }

    /// <summary>
    /// Hysteresis threshold for analog buttons/triggers.
    /// Prevents flickering when a value hovers near the activation point.
    /// Inspired by Bevy's ButtonSettings.
    /// </summary>
    public static bool ApplyHysteresis(float value, bool currentState, float pressThreshold, float releaseThreshold)
    {
        if (currentState) return value >= releaseThreshold;
        return value >= pressThreshold;
    }
}
