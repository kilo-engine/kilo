using System.Numerics;

namespace Kilo.Input.Actions;

/// <summary>
/// Runtime state for a single input action.
/// Tracks current/previous activation for edge detection (JustPressed/JustReleased).
/// Inspired by Stride's simple query API and Unreal's FInputActionInstance.
/// </summary>
public sealed class InputAction
{
    public required string Name { get; init; }
    public required ActionType Type { get; init; }

    public bool IsActive;
    public bool WasActive;
    public float FloatValue;
    public Vector2 Vector2Value;

    public bool IsPressed => IsActive;
    public bool JustPressed => IsActive && !WasActive;
    public bool JustReleased => !IsActive && WasActive;

    /// <summary>
    /// Saves current state as "previous" for next frame's edge detection.
    /// Call at the start of each frame before computing new state.
    /// </summary>
    public void SavePrevious()
    {
        WasActive = IsActive;
    }
}
