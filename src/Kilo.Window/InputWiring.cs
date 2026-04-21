using System.Numerics;
using Kilo.ECS;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Kilo.Window;

public static class InputWiring
{
    public static void WireInputEvents(IWindow window, KiloWorld world)
    {
        if (!world.HasResource<InputState>()) return;

        var inputState = world.GetResource<InputState>();
        var inputContext = window.CreateInput();

        WireKeyboard(inputContext, inputState);
        WireMouse(inputContext, inputState);
        WireGamepads(inputContext, inputState);
    }

    private static void WireKeyboard(IInputContext inputContext, InputState inputState)
    {
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
    }

    private static void WireMouse(IInputContext inputContext, InputState inputState)
    {
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
                if (idx >= 0 && idx < 8)
                {
                    inputState.MouseButtonsDown[idx] = true;
                    inputState.MouseButtonsPressed[idx] = true;
                }
            };
            mouse.MouseUp += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 8)
                {
                    inputState.MouseButtonsDown[idx] = false;
                    inputState.MouseButtonsReleased[idx] = true;
                }
            };
            mouse.Scroll += (_, offset) =>
            {
                inputState.ScrollDelta += (float)offset.Y;
            };
        }
    }

    private static void WireGamepads(IInputContext inputContext, InputState inputState)
    {
        foreach (var gamepad in inputContext.Gamepads)
        {
            int gpIndex = gamepad.Index;
            if (gpIndex < 0 || gpIndex >= 4) continue;

            inputState.Gamepads[gpIndex].IsConnected = true;
            inputState.ConnectedGamepadCount = Math.Max(inputState.ConnectedGamepadCount, gpIndex + 1);

            gamepad.ButtonDown += (_, button) =>
            {
                int idx = (int)button.Name; // GamepadButton enum
                if (idx >= 0 && idx < 16)
                {
                    inputState.Gamepads[gpIndex].ButtonsDown[idx] = true;
                    inputState.Gamepads[gpIndex].ButtonsPressed[idx] = true;
                }
            };
            gamepad.ButtonUp += (_, button) =>
            {
                int idx = (int)button.Name;
                if (idx >= 0 && idx < 16)
                {
                    inputState.Gamepads[gpIndex].ButtonsDown[idx] = false;
                    inputState.Gamepads[gpIndex].ButtonsReleased[idx] = true;
                }
            };
            gamepad.ThumbstickMoved += (_, thumbstick) =>
            {
                float x = (float)thumbstick.X;
                float y = (float)thumbstick.Y;
                bool isLeft = thumbstick.Index == 0;
                float dz = isLeft
                    ? inputState.Gamepads[gpIndex].LeftStickDeadZone
                    : inputState.Gamepads[gpIndex].RightStickDeadZone;
                var result = InputProcessing.ApplyDeadZone(x, y, dz);
                if (isLeft)
                    inputState.Gamepads[gpIndex].LeftStick = result;
                else
                    inputState.Gamepads[gpIndex].RightStick = result;
            };
            gamepad.TriggerMoved += (_, trigger) =>
            {
                float value = trigger.Position > inputState.Gamepads[gpIndex].TriggerThreshold
                    ? trigger.Position : 0f;
                if (trigger.Index == 0)
                    inputState.Gamepads[gpIndex].LeftTrigger = value;
                else
                    inputState.Gamepads[gpIndex].RightTrigger = value;
            };
        }
    }
}
