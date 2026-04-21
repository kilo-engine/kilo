namespace Kilo.Input.Bindings;

/// <summary>
/// Composite binding that combines 4 directional keys into a Vector2.
/// Inspired by Unity's CompositeBinding("WASD") — one binding instead of 4 separate Swizzle/Negate chains.
/// </summary>
public struct CompositeAxis2D
{
    public int UpKey;
    public int DownKey;
    public int LeftKey;
    public int RightKey;
    public int GamepadIndex;  // -1 = any gamepad
    public GamepadThumbstick FallbackStick;

    public CompositeAxis2D() { GamepadIndex = -1; FallbackStick = GamepadThumbstick.LeftStick; }
}
