using System.Numerics;

namespace Kilo.Window;

/// <summary>
/// Current frame input state for keyboard, mouse, and gamepad.
/// Frame-bounded: per-frame deltas (Pressed/Released/Delta/Scroll) are cleared each frame.
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

    /// <summary>Mouse buttons currently held down.</summary>
    public bool[] MouseButtonsDown { get; } = new bool[8];

    /// <summary>Mouse buttons pressed this frame (cleared on ResetFrame).</summary>
    public bool[] MouseButtonsPressed { get; } = new bool[8];

    /// <summary>Mouse buttons released this frame (cleared on ResetFrame).</summary>
    public bool[] MouseButtonsReleased { get; } = new bool[8];

    /// <summary>Scroll wheel delta since last frame (cleared on ResetFrame).</summary>
    public float ScrollDelta;

    // Gamepad state
    /// <summary>Up to 4 connected gamepads.</summary>
    public GamepadState[] Gamepads { get; } =
    [
        new(), new(), new(), new(),
    ];

    /// <summary>Number of connected gamepads.</summary>
    public int ConnectedGamepadCount;

    /// <summary>Whether the mouse cursor is locked to the window.</summary>
    public bool IsMouseLocked;

    /// <summary>Characters typed this frame (cleared on ResetFrame).</summary>
    public List<char> TextInput { get; } = new(32);

    /// <summary>Check if a key is currently held down.</summary>
    public bool IsKeyDown(int key) => KeysDown[key];

    /// <summary>Check if a key was pressed this frame.</summary>
    public bool IsKeyPressed(int key) => KeysPressed[key];

    /// <summary>Check if a key was released this frame.</summary>
    public bool IsKeyReleased(int key) => KeysReleased[key];

    /// <summary>Check if a mouse button is currently held down.</summary>
    public bool IsMouseButtonDown(int button) => MouseButtonsDown[button];

    /// <summary>
    /// Reset frame-specific state. Clears Pressed/Released arrays and deltas.
    /// Call at the end of each frame after all systems have read input.
    /// </summary>
    public void ResetFrame()
    {
        KeysPressed.AsSpan().Clear();
        KeysReleased.AsSpan().Clear();
        MouseButtonsPressed.AsSpan().Clear();
        MouseButtonsReleased.AsSpan().Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0f;
        TextInput.Clear();
        for (int i = 0; i < ConnectedGamepadCount; i++)
            Gamepads[i].ResetFrame();
    }
}
