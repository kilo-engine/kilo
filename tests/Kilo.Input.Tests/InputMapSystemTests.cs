using System.Numerics;
using Kilo.Input.Actions;
using Kilo.Input.Bindings;
using Kilo.Input.Contexts;
using Kilo.Input.Systems;
using Kilo.Window;
using Xunit;

namespace Kilo.Input.Tests;

public class InputMapSystemTests
{
    private static (InputMapStack stack, InputMapSystem system, InputState input) CreateSut()
    {
        var stack = new InputMapStack();
        var system = new InputMapSystem();
        var input = new InputState();
        return (stack, system, input);
    }

    [Fact]
    public void KeyboardButton_Pressed_Detected()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);
        stack.Register(map);
        stack.Enable("Player");

        // Simulate space pressed
        input.KeysDown[32] = true;
        input.KeysPressed[32] = true;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        Assert.True(stack.JustPressed("Jump"));
    }

    [Fact]
    public void KeyboardButton_NotPressed_Detected()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);
        stack.Register(map);
        stack.Enable("Player");

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        Assert.False(stack.IsPressed("Jump"));
    }

    [Fact]
    public void CompositeWASD_Works()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAxis2D("Move", 87, 83, 65, 68); // W S A D
        stack.Register(map);
        stack.Enable("Player");

        // Press W + D → (1, 1)
        input.KeysDown[87] = true; // W
        input.KeysDown[68] = true; // D

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        var move = stack.GetVector2("Move");
        Assert.True(MathF.Abs(move.X - 1f) < 0.01f, $"Expected X=1, got {move.X}");
        Assert.True(MathF.Abs(move.Y - 1f) < 0.01f, $"Expected Y=1, got {move.Y}");
    }

    [Fact]
    public void MouseButton_Pressed_Detected()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Fire", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Mouse, KeyCode = 0 },
        ]);
        stack.Register(map);
        stack.Enable("Player");

        input.MouseButtonsDown[0] = true;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        Assert.True(stack.IsPressed("Fire"));
    }

    [Fact]
    public void ModifierScale_AppliedToVector2()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAxis2D("Move", 87, 83, 65, 68,
            modifiers: [new Modifiers.ScaleModifier { Factor = 5.0f }]);
        stack.Register(map);
        stack.Enable("Player");

        input.KeysDown[68] = true; // D → X=1

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        var move = stack.GetVector2("Move");
        Assert.True(MathF.Abs(move.X - 5.0f) < 0.01f, $"Expected X=5, got {move.X}");
    }

    [Fact]
    public void GamepadButton_Pressed_Detected()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.GamepadButton, GamepadButton = 0, GamepadIndex = -1 },
        ]);
        stack.Register(map);
        stack.Enable("Player");

        input.Gamepads[0].IsConnected = true;
        input.Gamepads[0].ButtonsDown[0] = true;
        input.ConnectedGamepadCount = 1;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        Assert.True(stack.IsPressed("Jump"));
    }

    [Fact]
    public void GamepadStick_Axis2D_Works()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAxis2D("Move", 87, 83, 65, 68,
            stick: GamepadThumbstick.LeftStick);
        stack.Register(map);
        stack.Enable("Player");

        input.Gamepads[0].IsConnected = true;
        input.Gamepads[0].LeftStick = new Vector2(0.7f, -0.3f);
        input.ConnectedGamepadCount = 1;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        var move = stack.GetVector2("Move");
        Assert.True(MathF.Abs(move.X - 0.7f) < 0.01f);
        Assert.True(MathF.Abs(move.Y - (-0.3f)) < 0.01f);
    }

    [Fact]
    public void HigherPriority_ConsumesAction()
    {
        var (stack, system, input) = CreateSut();

        var ui = new InputMap("UI", 10);
        ui.AddAction("Escape", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 256 },
        ]);

        var player = new InputMap("Player", 0);
        player.AddAction("Escape", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 256 },
        ]);

        stack.Register(ui);
        stack.Register(player);
        stack.Enable("UI");
        stack.Enable("Player");

        input.KeysDown[256] = true;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        // UI consumed "Escape", Player's "Escape" should be suppressed
        Assert.True(stack.IsPressed("Escape"));
    }

    [Fact]
    public void DisabledMap_NotProcessed()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);
        stack.Register(map);
        // Not enabled

        input.KeysDown[32] = true;

        stack.BeginFrame();
        system.Update(input, stack, 0.016f);

        Assert.False(stack.IsPressed("Jump"));
    }

    [Fact]
    public void JustPressed_OneFrameOnly()
    {
        var (stack, system, input) = CreateSut();
        var map = new InputMap("Player", 0);
        map.AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = 32 },
        ]);
        stack.Register(map);
        stack.Enable("Player");

        // Frame 1: press
        input.KeysDown[32] = true;
        stack.BeginFrame();
        system.Update(input, stack, 0.016f);
        Assert.True(stack.JustPressed("Jump"));

        // Frame 2: still held
        stack.BeginFrame();
        system.Update(input, stack, 0.016f);
        Assert.False(stack.JustPressed("Jump"));
        Assert.True(stack.IsPressed("Jump"));
    }
}
