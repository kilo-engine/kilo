using System.Numerics;

namespace Kilo.Window;

/// <summary>
/// State for a single gamepad.
/// Button indices match standard layout: South/East/West/North/Shoulders/Triggers/DPad/Select/Start.
/// </summary>
public struct GamepadState
{
    public bool IsConnected;

    public Vector2 LeftStick;
    public Vector2 RightStick;
    public float LeftTrigger;
    public float RightTrigger;

    public bool[] ButtonsDown { get; } = new bool[16];
    public bool[] ButtonsPressed { get; } = new bool[16];
    public bool[] ButtonsReleased { get; } = new bool[16];

    public float LeftStickDeadZone;
    public float RightStickDeadZone;
    public float TriggerThreshold;

    public float VibrationLeftMotor;
    public float VibrationRightMotor;

    public GamepadState()
    {
        LeftStickDeadZone = 0.15f;
        RightStickDeadZone = 0.15f;
        TriggerThreshold = 0.1f;
    }

    public void ResetFrame()
    {
        ButtonsPressed.AsSpan().Clear();
        ButtonsReleased.AsSpan().Clear();
    }
}
