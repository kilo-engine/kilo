using System.Numerics;
using Kilo.ECS;
using Kilo.Input;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Kilo.Rendering;

public static class InputWiring
{
    public static void WireInputEvents(IWindow window, KiloWorld world)
    {
        if (!world.HasResource<InputState>()) return;

        var inputState = world.GetResource<InputState>();
        var inputContext = window.CreateInput();

        foreach (var keyboard in inputContext.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                int code = (int)key;
                if (code >= 0 && code < 512)
                {
                    inputState.KeysDown[code] = true;
                    inputState.KeysPressed[code] = true;
                }
            };
            keyboard.KeyUp += (_, key, _) =>
            {
                int code = (int)key;
                if (code >= 0 && code < 512)
                {
                    inputState.KeysDown[code] = false;
                    inputState.KeysReleased[code] = true;
                }
            };
        }

        foreach (var mouse in inputContext.Mice)
        {
            mouse.MouseMove += (_, position) =>
            {
                var newPos = new Vector2((float)position.X, (float)position.Y);
                inputState.MouseDelta += newPos - inputState.MousePosition;
                inputState.MousePosition = newPos;
            };
            mouse.MouseDown += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 5)
                    inputState.MouseButtonsDown[idx] = true;
            };
            mouse.MouseUp += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 5)
                    inputState.MouseButtonsDown[idx] = false;
            };
            mouse.Scroll += (_, offset) =>
            {
                inputState.ScrollDelta += (float)offset.Y;
            };
        }
    }
}
