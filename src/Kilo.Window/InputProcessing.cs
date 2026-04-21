using System.Numerics;

namespace Kilo.Window;

/// <summary>
/// Input processing utilities: dead zones, hysteresis.
/// Used by InputWiring at the platform layer.
/// </summary>
public static class InputProcessing
{
    /// <summary>
    /// Radial dead zone with linear remapping for thumbsticks.
    /// </summary>
    public static Vector2 ApplyDeadZone(float x, float y, float deadZone)
    {
        float magnitude = MathF.Sqrt(x * x + y * y);
        if (magnitude <= deadZone) return Vector2.Zero;

        float remapped = (magnitude - deadZone) / (1.0f - deadZone);
        float scale = remapped / magnitude;
        return new Vector2(x * scale, y * scale);
    }

    /// <summary>
    /// Hysteresis for analog triggers to prevent flickering near threshold.
    /// </summary>
    public static bool ApplyHysteresis(float value, bool currentState, float pressThreshold, float releaseThreshold)
    {
        if (currentState) return value >= releaseThreshold;
        return value >= pressThreshold;
    }
}
