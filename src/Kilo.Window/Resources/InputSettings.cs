namespace Kilo.Window;

/// <summary>
/// Input configuration settings.
/// </summary>
public sealed class InputSettings
{
    /// <summary>Mouse sensitivity multiplier.</summary>
    public float MouseSensitivity { get; set; } = 1.0f;

    /// <summary>Gamepad analog stick dead zone threshold (0-1).</summary>
    public float GamepadDeadZone { get; set; } = 0.1f;
}
