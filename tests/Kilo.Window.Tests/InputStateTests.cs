using System.Numerics;
using Kilo.Window;
using Xunit;

namespace Kilo.Window.Tests;

public class InputStateTests
{
    [Fact]
    public void DefaultState_AllKeysUp()
    {
        var state = new InputState();

        // All keys should be up by default
        Assert.All(state.KeysDown, key => Assert.False(key));
        Assert.All(state.KeysPressed, key => Assert.False(key));
        Assert.All(state.KeysReleased, key => Assert.False(key));
    }

    [Fact]
    public void DefaultState_AllMouseButtonsUp()
    {
        var state = new InputState();

        // All mouse buttons should be up by default
        Assert.All(state.MouseButtonsDown, button => Assert.False(button));
    }

    [Fact]
    public void DefaultState_MouseAndScrollZero()
    {
        var state = new InputState();

        Assert.Equal(Vector2.Zero, state.MousePosition);
        Assert.Equal(Vector2.Zero, state.MouseDelta);
        Assert.Equal(0f, state.ScrollDelta);
    }

    [Fact]
    public void IsKeyDown_ReturnsCorrectState()
    {
        var state = new InputState();
        state.KeysDown[10] = true;

        Assert.True(state.IsKeyDown(10));
        Assert.False(state.IsKeyDown(20));
    }

    [Fact]
    public void IsKeyPressed_ReturnsCorrectState()
    {
        var state = new InputState();
        state.KeysPressed[10] = true;

        Assert.True(state.IsKeyPressed(10));
        Assert.False(state.IsKeyPressed(20));
    }

    [Fact]
    public void IsKeyReleased_ReturnsCorrectState()
    {
        var state = new InputState();
        state.KeysReleased[10] = true;

        Assert.True(state.IsKeyReleased(10));
        Assert.False(state.IsKeyReleased(20));
    }

    [Fact]
    public void IsMouseButtonDown_ReturnsCorrectState()
    {
        var state = new InputState();
        state.MouseButtonsDown[0] = true;

        Assert.True(state.IsMouseButtonDown(0));
        Assert.False(state.IsMouseButtonDown(1));
    }

    [Fact]
    public void ResetFrame_ClearsPressedReleasedArrays()
    {
        var state = new InputState();
        state.KeysPressed[10] = true;
        state.KeysReleased[20] = true;

        state.ResetFrame();

        Assert.False(state.KeysPressed[10]);
        Assert.False(state.KeysReleased[20]);
    }

    [Fact]
    public void ResetFrame_PreservesKeysDown()
    {
        var state = new InputState();
        state.KeysDown[10] = true;

        state.ResetFrame();

        Assert.True(state.KeysDown[10]);
    }

    [Fact]
    public void ResetFrame_ClearsMouseDelta()
    {
        var state = new InputState();
        state.MouseDelta = new Vector2(5, 3);

        state.ResetFrame();

        Assert.Equal(Vector2.Zero, state.MouseDelta);
    }

    [Fact]
    public void ResetFrame_ClearsScrollDelta()
    {
        var state = new InputState();
        state.ScrollDelta = 1.5f;

        state.ResetFrame();

        Assert.Equal(0f, state.ScrollDelta);
    }

    [Fact]
    public void ResetFrame_PreservesMousePosition()
    {
        var state = new InputState();
        state.MousePosition = new Vector2(100, 200);

        state.ResetFrame();

        Assert.Equal(new Vector2(100, 200), state.MousePosition);
    }

    [Fact]
    public void ResetFrame_PreservesMouseButtonsDown()
    {
        var state = new InputState();
        state.MouseButtonsDown[0] = true;

        state.ResetFrame();

        Assert.True(state.MouseButtonsDown[0]);
    }
}
