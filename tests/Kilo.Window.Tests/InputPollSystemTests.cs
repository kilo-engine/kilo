using System.Numerics;
using Kilo.ECS;
using Kilo.Window;
using Xunit;

namespace Kilo.Window.Tests;

public class InputPollSystemTests
{
    [Fact]
    public void InputPollSystem_Update_CallsResetFrame()
    {
        var world = new KiloWorld();
        var inputState = new InputState();
        world.AddResource(inputState);

        // Set some per-frame state
        inputState.KeysPressed[10] = true;
        inputState.KeysReleased[20] = true;
        inputState.MouseDelta = new Vector2(5, 3);
        inputState.ScrollDelta = 1.5f;

        var system = new InputPollSystem();
        system.Update(world);

        // Verify per-frame state was cleared
        Assert.False(inputState.KeysPressed[10]);
        Assert.False(inputState.KeysReleased[20]);
        Assert.Equal(Vector2.Zero, inputState.MouseDelta);
        Assert.Equal(0f, inputState.ScrollDelta);
    }

    [Fact]
    public void InputPollSystem_Update_PreservesPersistentState()
    {
        var world = new KiloWorld();
        var inputState = new InputState();
        world.AddResource(inputState);

        // Set some persistent state
        inputState.KeysDown[10] = true;
        inputState.MouseButtonsDown[0] = true;
        inputState.MousePosition = new Vector2(100, 200);

        var system = new InputPollSystem();
        system.Update(world);

        // Verify persistent state was preserved
        Assert.True(inputState.KeysDown[10]);
        Assert.True(inputState.MouseButtonsDown[0]);
        Assert.Equal(new Vector2(100, 200), inputState.MousePosition);
    }

    [Fact]
    public void InputPollSystem_MultipleUpdates_EachResetsFrame()
    {
        var world = new KiloWorld();
        var inputState = new InputState();
        world.AddResource(inputState);

        var system = new InputPollSystem();

        // First update
        inputState.KeysPressed[10] = true;
        system.Update(world);
        Assert.False(inputState.KeysPressed[10]);

        // Second update - should also clear
        inputState.KeysPressed[20] = true;
        system.Update(world);
        Assert.False(inputState.KeysPressed[20]);
    }
}
