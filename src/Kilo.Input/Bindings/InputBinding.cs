using Kilo.Input.Modifiers;

namespace Kilo.Input.Bindings;

/// <summary>
/// A single physical input binding.
/// Maps one physical key/button/axis to a logical action.
/// Inspired by Unreal's per-key mapping within InputMappingContext.
/// </summary>
public struct InputBinding
{
    public BindingSourceType SourceType;
    public int KeyCode;
    public int GamepadIndex;  // -1 = any gamepad
    public int GamepadButton;
    public GamepadAxis GamepadAxis;
    public GamepadThumbstick GamepadThumbstick;

    public InputBinding() { GamepadIndex = -1; }
}
