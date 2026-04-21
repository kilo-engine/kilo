using System.Numerics;
using Kilo.Input.Actions;
using Kilo.Input.Bindings;
using Kilo.Input.Contexts;
using Xunit;

namespace Kilo.Input.Tests;

public class InputMapStackTests
{
    [Fact]
    public void RegisterAndEnable_Works()
    {
        var stack = new InputMapStack();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);

        stack.Register(map);
        stack.Enable("Player");

        Assert.Single(stack.ActiveMaps);
    }

    [Fact]
    public void Disable_RemovesFromActive()
    {
        var stack = new InputMapStack();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);

        stack.Register(map);
        stack.Enable("Player");
        stack.Disable("Player");

        Assert.Empty(stack.ActiveMaps);
    }

    [Fact]
    public void PriorityOrdering_HigherFirst()
    {
        var stack = new InputMapStack();
        var low = new InputMap("Player", 0);
        var high = new InputMap("UI", 10);

        stack.Register(low);
        stack.Register(high);
        stack.Enable("Player");
        stack.Enable("UI");

        var active = stack.ActiveMaps.ToList();
        Assert.Equal("UI", active[0].Name);
        Assert.Equal("Player", active[1].Name);
    }

    [Fact]
    public void ActionState_JustPressed()
    {
        var stack = new InputMapStack();
        stack.UpdateActionState("Jump", ActionType.Button, true, Vector2.Zero, 1f);

        Assert.True(stack.JustPressed("Jump"));
        Assert.False(stack.JustReleased("Jump"));
        Assert.True(stack.IsPressed("Jump"));
    }

    [Fact]
    public void ActionState_JustReleased()
    {
        var stack = new InputMapStack();

        // Frame 1: press
        stack.UpdateActionState("Jump", ActionType.Button, true, Vector2.Zero, 1f);
        Assert.True(stack.JustPressed("Jump"));

        // Frame 2: release (BeginFrame saves WasActive=true)
        stack.BeginFrame();
        stack.UpdateActionState("Jump", ActionType.Button, false, Vector2.Zero, 0f);

        Assert.True(stack.JustReleased("Jump"));
        Assert.False(stack.JustPressed("Jump"));
        Assert.False(stack.IsPressed("Jump"));
    }

    [Fact]
    public void ActionState_Axis2D()
    {
        var stack = new InputMapStack();
        stack.UpdateActionState("Move", ActionType.Axis2D, true, new Vector2(1f, 0.5f), 0f);

        Assert.Equal(new Vector2(1f, 0.5f), stack.GetVector2("Move"));
    }

    [Fact]
    public void ActionState_Axis1D()
    {
        var stack = new InputMapStack();
        stack.UpdateActionState("Throttle", ActionType.Axis1D, true, Vector2.Zero, 0.8f);

        Assert.Equal(0.8f, stack.GetFloat("Throttle"));
    }

    [Fact]
    public void MissingAction_ReturnsDefault()
    {
        var stack = new InputMapStack();
        Assert.False(stack.IsPressed("Nonexistent"));
        Assert.Equal(Vector2.Zero, stack.GetVector2("Nonexistent"));
        Assert.Equal(0f, stack.GetFloat("Nonexistent"));
    }

    [Fact]
    public void Consumption_BlocksLowerPriority()
    {
        var stack = new InputMapStack();
        var high = new InputMap("UI", 10);
        high.AddAction("Escape", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 256 },
        ]);
        var low = new InputMap("Player", 0);
        low.AddAction("Escape", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 256 },
        ]);

        stack.Register(high);
        stack.Register(low);
        stack.Enable("UI");
        stack.Enable("Player");

        // High priority consumes the action
        stack.ConsumeAction("Escape");
        Assert.True(stack.IsActionConsumed("Escape"));

        // Lower priority should see it as consumed
        Assert.True(stack.IsActionConsumed("Escape"));
    }
}
