using System.Numerics;
using Kilo.Input.Actions;
using Kilo.Input.Bindings;
using Kilo.Input.Contexts;
using Kilo.Input.Modifiers;
using Kilo.Input.Triggers;
using Xunit;

namespace Kilo.Input.Tests;

public class InputMapTests
{
    [Fact]
    public void AddAction_Button_Works()
    {
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);

        var actions = map.Actions;
        Assert.Single(actions);
        Assert.Equal(ActionType.Button, actions["Jump"].Type);
        Assert.Single(actions["Jump"].Bindings);
    }

    [Fact]
    public void AddAxis2D_Works()
    {
        var map = new InputMap("Player", 0);
        map.AddAxis2D("Move", 87, 83, 65, 68); // WASD

        var actions = map.Actions;
        Assert.True(actions.ContainsKey("Move"));
        Assert.Equal(ActionType.Axis2D, actions["Move"].Type);
        Assert.NotNull(actions["Move"].Composite);
    }

    [Fact]
    public void AddAction_WithModifiers_Works()
    {
        var map = new InputMap("Player", 0);
        map.AddAction("MoveUp", ActionType.Axis1D,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 69 },
        ],
        modifiers: [new ScaleModifier { Factor = 2.0f }]);

        Assert.Single(map.Actions["MoveUp"].Modifiers);
    }

    [Fact]
    public void AddAction_WithTrigger_Works()
    {
        var map = new InputMap("Player", 0);
        map.AddAction("Charge", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 70 },
        ],
        trigger: new HoldTrigger { Duration = 0.5f });

        Assert.IsType<HoldTrigger>(map.Actions["Charge"].Trigger);
    }

    [Fact]
    public void AddAxis2D_WithGamepadFallback_Works()
    {
        var map = new InputMap("Player", 0);
        map.AddAxis2D("Move", 87, 83, 65, 68,
            stick: GamepadThumbstick.LeftStick,
            gamepadIndex: 0);

        var composite = map.Actions["Move"].Composite!.Value;
        Assert.Equal(0, composite.GamepadIndex);
        Assert.Equal(GamepadThumbstick.LeftStick, composite.FallbackStick);
    }
}
