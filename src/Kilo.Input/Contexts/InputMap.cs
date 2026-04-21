using Kilo.Input.Actions;
using Kilo.Input.Bindings;
using Kilo.Input.Modifiers;
using Kilo.Input.Triggers;

namespace Kilo.Input.Contexts;

/// <summary>
/// Defines a named group of action→binding mappings with a priority.
/// Inspired by Unreal's InputMappingContext + Bevy's Context.
/// Higher priority maps are evaluated first and can consume inputs.
/// </summary>
public sealed class InputMap
{
    public string Name { get; }
    public int Priority { get; }

    private readonly Dictionary<string, ActionDef> _actions = new();

    public InputMap(string name, int priority = 0)
    {
        Name = name;
        Priority = priority;
    }

    /// <summary>Read-only view of all action definitions in this map.</summary>
    public IReadOnlyDictionary<string, ActionDef> Actions => _actions;

    /// <summary>
    /// Adds an action with explicit bindings.
    /// </summary>
    public InputMap AddAction(string name, ActionType type,
        InputBinding[] bindings,
        IInputModifier[]? modifiers = null,
        IInputTrigger? trigger = null)
    {
        _actions[name] = new ActionDef
        {
            Name = name,
            Type = type,
            Bindings = bindings,
            Modifiers = modifiers ?? [],
            Trigger = trigger ?? new PressTrigger(),
        };
        return this;
    }

    /// <summary>
    /// Adds a 2D axis action from 4 directional keys + optional gamepad stick fallback.
    /// Inspired by Unity's CompositeBinding("WASD").
    /// </summary>
    public InputMap AddAxis2D(string name,
        int upKey, int downKey, int leftKey, int rightKey,
        GamepadThumbstick stick = GamepadThumbstick.LeftStick,
        int gamepadIndex = -1,
        IInputModifier[]? modifiers = null)
    {
        _actions[name] = new ActionDef
        {
            Name = name,
            Type = ActionType.Axis2D,
            Bindings = [],
            Composite = new CompositeAxis2D
            {
                UpKey = upKey, DownKey = downKey,
                LeftKey = leftKey, RightKey = rightKey,
                GamepadIndex = gamepadIndex,
                FallbackStick = stick,
            },
            Modifiers = modifiers ?? [],
            Trigger = new PressTrigger(),
        };
        return this;
    }
}
