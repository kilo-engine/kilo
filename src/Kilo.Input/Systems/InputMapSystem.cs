using System.Numerics;
using Kilo.Input.Actions;
using Kilo.Input.Bindings;
using Kilo.Input.Contexts;
using Kilo.Input.Triggers;
using Kilo.Window;

namespace Kilo.Input.Systems;

/// <summary>
/// Core pipeline: Raw InputState → Bindings → Modifier chain → Trigger → Action state.
/// Processes all active InputMaps in priority order, consuming conflicting inputs.
/// Inspired by Unreal's Enhanced Input pipeline + Bevy's EnhancedInputSystems.
/// </summary>
public sealed class InputMapSystem
{
    /// <summary>
    /// Evaluates all active maps and updates action states.
    /// Call once per frame, after BeginFrame() on the stack.
    /// </summary>
    public void Update(InputState input, InputMapStack stack, float deltaTime)
    {
        var consumedKeys = new HashSet<int>();
        var consumedMouseButtons = new HashSet<int>();
        var consumedGamepadButtons = new HashSet<(int gp, int btn)>();

        foreach (var map in stack.ActiveMaps)
        {
            foreach (var (actionName, def) in map.Actions)
            {
                if (stack.IsActionConsumed(actionName)) continue;

                EvaluateAction(input, stack, def, deltaTime,
                    consumedKeys, consumedMouseButtons, consumedGamepadButtons);
            }
        }
    }

    private static void EvaluateAction(
        InputState input, InputMapStack stack, ActionDef def,
        float deltaTime,
        HashSet<int> consumedKeys, HashSet<int> consumedMouseButtons,
        HashSet<(int, int)> consumedGamepadButtons)
    {
        float rawFloat = 0f;
        Vector2 rawVec2 = Vector2.Zero;
        bool found = false;

        // Composite binding (WASD → Vector2)
        if (def.Composite.HasValue)
        {
            rawVec2 = EvaluateComposite(def.Composite.Value, input, consumedKeys);
            found = rawVec2 != Vector2.Zero;

            // Gamepad stick fallback
            if (!found)
            {
                rawVec2 = EvaluateThumbstick(def.Composite.Value, input);
                found = rawVec2 != Vector2.Zero;
            }
        }

        // Standard bindings
        if (!found)
        {
            foreach (var binding in def.Bindings)
            {
                float value = EvaluateBinding(binding, input, consumedKeys, consumedMouseButtons, consumedGamepadButtons);
                if (value > 0f)
                {
                    rawFloat = value;
                    found = true;
                    break;
                }
            }
        }

        // Modifier chain
        foreach (var mod in def.Modifiers)
        {
            rawFloat = mod.ModifyFloat(rawFloat, deltaTime);
            rawVec2 = mod.ModifyVector2(rawVec2, deltaTime);
        }

        // Trigger evaluation
        float magnitude = def.Type == ActionType.Axis2D
            ? rawVec2.Length()
            : rawFloat;
        var triggerState = def.Trigger.Update(found ? MathF.Max(magnitude, 0.001f) : 0f, deltaTime);
        bool isActive = triggerState == TriggerState.Triggered;

        // Write action state
        stack.UpdateActionState(def.Name, def.Type, isActive,
            def.Type == ActionType.Axis2D ? rawVec2 : Vector2.Zero,
            rawFloat);

        if (isActive) stack.ConsumeAction(def.Name);
    }

    private static Vector2 EvaluateComposite(CompositeAxis2D c, InputState input, HashSet<int> consumed)
    {
        float x = 0f, y = 0f;
        if (!consumed.Contains(c.RightKey) && input.KeysDown[c.RightKey]) x += 1f;
        if (!consumed.Contains(c.LeftKey) && input.KeysDown[c.LeftKey]) x -= 1f;
        if (!consumed.Contains(c.UpKey) && input.KeysDown[c.UpKey]) y += 1f;
        if (!consumed.Contains(c.DownKey) && input.KeysDown[c.DownKey]) y -= 1f;
        return new Vector2(x, y);
    }

    private static Vector2 EvaluateThumbstick(CompositeAxis2D c, InputState input)
    {
        for (int i = 0; i < input.ConnectedGamepadCount; i++)
        {
            if (c.GamepadIndex >= 0 && i != c.GamepadIndex) continue;
            if (!input.Gamepads[i].IsConnected) continue;

            var stick = c.FallbackStick == GamepadThumbstick.LeftStick
                ? input.Gamepads[i].LeftStick
                : input.Gamepads[i].RightStick;

            if (stick.LengthSquared() > 0.001f) return stick;
        }
        return Vector2.Zero;
    }

    private static float EvaluateBinding(InputBinding b, InputState input,
        HashSet<int> consumedKeys, HashSet<int> consumedMouseButtons,
        HashSet<(int, int)> consumedGamepadButtons)
    {
        return b.SourceType switch
        {
            BindingSourceType.Keyboard when !consumedKeys.Contains(b.KeyCode)
                => input.KeysDown[b.KeyCode] ? 1f : 0f,
            BindingSourceType.Mouse when !consumedMouseButtons.Contains(b.KeyCode)
                => input.MouseButtonsDown[b.KeyCode] ? 1f : 0f,
            BindingSourceType.GamepadButton => EvaluateGamepadButton(b, input, consumedGamepadButtons),
            BindingSourceType.GamepadAxis => EvaluateGamepadAxis(b, input),
            _ => 0f,
        };
    }

    private static float EvaluateGamepadButton(InputBinding b, InputState input,
        HashSet<(int, int)> consumed)
    {
        for (int i = 0; i < input.ConnectedGamepadCount; i++)
        {
            if (b.GamepadIndex >= 0 && i != b.GamepadIndex) continue;
            if (!input.Gamepads[i].IsConnected) continue;
            if (consumed.Contains((i, b.GamepadButton))) continue;
            if (input.Gamepads[i].ButtonsDown[b.GamepadButton]) return 1f;
        }
        return 0f;
    }

    private static float EvaluateGamepadAxis(InputBinding b, InputState input)
    {
        for (int i = 0; i < input.ConnectedGamepadCount; i++)
        {
            if (b.GamepadIndex >= 0 && i != b.GamepadIndex) continue;
            if (!input.Gamepads[i].IsConnected) continue;
            return b.GamepadAxis switch
            {
                GamepadAxis.LeftTrigger => input.Gamepads[i].LeftTrigger,
                GamepadAxis.RightTrigger => input.Gamepads[i].RightTrigger,
                _ => 0f,
            };
        }
        return 0f;
    }
}
