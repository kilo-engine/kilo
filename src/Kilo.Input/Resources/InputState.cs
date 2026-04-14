using System.Numerics;

namespace Kilo.Input;

/// <summary>
/// Current frame input state for keyboard and mouse.
/// </summary>
public sealed class InputState
{
    // Keyboard state
    /// <summary>Keys currently held down.</summary>
    public bool[] KeysDown { get; } = new bool[512];

    /// <summary>Keys pressed this frame (cleared on ResetFrame).</summary>
    public bool[] KeysPressed { get; } = new bool[512];

    /// <summary>Keys released this frame (cleared on ResetFrame).</summary>
    public bool[] KeysReleased { get; } = new bool[512];

    // Mouse state
    /// <summary>Current mouse position in window coordinates.</summary>
    public Vector2 MousePosition;

    /// <summary>Mouse movement delta since last frame (cleared on ResetFrame).</summary>
    public Vector2 MouseDelta;

    /// <summary>Mouse buttons currently held down (5 buttons max).</summary>
    public bool[] MouseButtonsDown { get; } = new bool[5];

    /// <summary>Scroll wheel delta since last frame (cleared on ResetFrame).</summary>
    public float ScrollDelta;

    /// <summary>Check if a key is currently held down.</summary>
    public bool IsKeyDown(int key) => KeysDown[key];

    /// <summary>Check if a key was pressed this frame.</summary>
    public bool IsKeyPressed(int key) => KeysPressed[key];

    /// <summary>Check if a key was released this frame.</summary>
    public bool IsKeyReleased(int key) => KeysReleased[key];

    /// <summary>Check if a mouse button is currently held down.</summary>
    public bool IsMouseButtonDown(int button) => MouseButtonsDown[button];

    /// <summary>
    /// Reset frame-specific state. Copies KeysDown to previous state,
    /// clears Pressed/Released arrays, and resets MouseDelta/ScrollDelta.
    /// </summary>
    public void ResetFrame()
    {
        // Clear per-frame state
        KeysPressed.AsSpan().Clear();
        KeysReleased.AsSpan().Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0f;
    }
}
