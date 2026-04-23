using ImGuiNET;

namespace Kilo.Rendering;

/// <summary>
/// Maps Silk.NET Key enum values to ImGuiKey.
/// Used by ImGuiController to feed keyboard state from InputState.
/// </summary>
internal static class ImGuiKeyMap
{
    public static ImGuiKey Map(int keyCode)
    {
        if (keyCode < 0 || keyCode > 255) return ImGuiKey.None;

        return (Silk.NET.Input.Key)keyCode switch
        {
            Silk.NET.Input.Key.Unknown => ImGuiKey.None,

            // Digits
            Silk.NET.Input.Key.Number0 => ImGuiKey._0,
            Silk.NET.Input.Key.Number1 => ImGuiKey._1,
            Silk.NET.Input.Key.Number2 => ImGuiKey._2,
            Silk.NET.Input.Key.Number3 => ImGuiKey._3,
            Silk.NET.Input.Key.Number4 => ImGuiKey._4,
            Silk.NET.Input.Key.Number5 => ImGuiKey._5,
            Silk.NET.Input.Key.Number6 => ImGuiKey._6,
            Silk.NET.Input.Key.Number7 => ImGuiKey._7,
            Silk.NET.Input.Key.Number8 => ImGuiKey._8,
            Silk.NET.Input.Key.Number9 => ImGuiKey._9,

            // Letters
            Silk.NET.Input.Key.A => ImGuiKey.A,
            Silk.NET.Input.Key.B => ImGuiKey.B,
            Silk.NET.Input.Key.C => ImGuiKey.C,
            Silk.NET.Input.Key.D => ImGuiKey.D,
            Silk.NET.Input.Key.E => ImGuiKey.E,
            Silk.NET.Input.Key.F => ImGuiKey.F,
            Silk.NET.Input.Key.G => ImGuiKey.G,
            Silk.NET.Input.Key.H => ImGuiKey.H,
            Silk.NET.Input.Key.I => ImGuiKey.I,
            Silk.NET.Input.Key.J => ImGuiKey.J,
            Silk.NET.Input.Key.K => ImGuiKey.K,
            Silk.NET.Input.Key.L => ImGuiKey.L,
            Silk.NET.Input.Key.M => ImGuiKey.M,
            Silk.NET.Input.Key.N => ImGuiKey.N,
            Silk.NET.Input.Key.O => ImGuiKey.O,
            Silk.NET.Input.Key.P => ImGuiKey.P,
            Silk.NET.Input.Key.Q => ImGuiKey.Q,
            Silk.NET.Input.Key.R => ImGuiKey.R,
            Silk.NET.Input.Key.S => ImGuiKey.S,
            Silk.NET.Input.Key.T => ImGuiKey.T,
            Silk.NET.Input.Key.U => ImGuiKey.U,
            Silk.NET.Input.Key.V => ImGuiKey.V,
            Silk.NET.Input.Key.W => ImGuiKey.W,
            Silk.NET.Input.Key.X => ImGuiKey.X,
            Silk.NET.Input.Key.Y => ImGuiKey.Y,
            Silk.NET.Input.Key.Z => ImGuiKey.Z,

            // Function keys
            Silk.NET.Input.Key.F1 => ImGuiKey.F1,
            Silk.NET.Input.Key.F2 => ImGuiKey.F2,
            Silk.NET.Input.Key.F3 => ImGuiKey.F3,
            Silk.NET.Input.Key.F4 => ImGuiKey.F4,
            Silk.NET.Input.Key.F5 => ImGuiKey.F5,
            Silk.NET.Input.Key.F6 => ImGuiKey.F6,
            Silk.NET.Input.Key.F7 => ImGuiKey.F7,
            Silk.NET.Input.Key.F8 => ImGuiKey.F8,
            Silk.NET.Input.Key.F9 => ImGuiKey.F9,
            Silk.NET.Input.Key.F10 => ImGuiKey.F10,
            Silk.NET.Input.Key.F11 => ImGuiKey.F11,
            Silk.NET.Input.Key.F12 => ImGuiKey.F12,
            Silk.NET.Input.Key.F13 => ImGuiKey.F13,
            Silk.NET.Input.Key.F14 => ImGuiKey.F14,
            Silk.NET.Input.Key.F15 => ImGuiKey.F15,
            Silk.NET.Input.Key.F16 => ImGuiKey.F16,
            Silk.NET.Input.Key.F17 => ImGuiKey.F17,
            Silk.NET.Input.Key.F18 => ImGuiKey.F18,
            Silk.NET.Input.Key.F19 => ImGuiKey.F19,
            Silk.NET.Input.Key.F20 => ImGuiKey.F20,
            Silk.NET.Input.Key.F21 => ImGuiKey.F21,
            Silk.NET.Input.Key.F22 => ImGuiKey.F22,
            Silk.NET.Input.Key.F23 => ImGuiKey.F23,
            Silk.NET.Input.Key.F24 => ImGuiKey.F24,

            // Special keys
            Silk.NET.Input.Key.Enter => ImGuiKey.Enter,
            Silk.NET.Input.Key.Escape => ImGuiKey.Escape,
            Silk.NET.Input.Key.Backspace => ImGuiKey.Backspace,
            Silk.NET.Input.Key.Tab => ImGuiKey.Tab,
            Silk.NET.Input.Key.Space => ImGuiKey.Space,
            Silk.NET.Input.Key.Insert => ImGuiKey.Insert,
            Silk.NET.Input.Key.Delete => ImGuiKey.Delete,
            Silk.NET.Input.Key.Home => ImGuiKey.Home,
            Silk.NET.Input.Key.End => ImGuiKey.End,
            Silk.NET.Input.Key.PageUp => ImGuiKey.PageUp,
            Silk.NET.Input.Key.PageDown => ImGuiKey.PageDown,
            Silk.NET.Input.Key.PrintScreen => ImGuiKey.PrintScreen,
            Silk.NET.Input.Key.Pause => ImGuiKey.Pause,

            // Arrow keys
            Silk.NET.Input.Key.Left => ImGuiKey.LeftArrow,
            Silk.NET.Input.Key.Right => ImGuiKey.RightArrow,
            Silk.NET.Input.Key.Up => ImGuiKey.UpArrow,
            Silk.NET.Input.Key.Down => ImGuiKey.DownArrow,

            // Modifiers
            Silk.NET.Input.Key.ShiftLeft => ImGuiKey.LeftShift,
            Silk.NET.Input.Key.ShiftRight => ImGuiKey.RightShift,
            Silk.NET.Input.Key.ControlLeft => ImGuiKey.LeftCtrl,
            Silk.NET.Input.Key.ControlRight => ImGuiKey.RightCtrl,
            Silk.NET.Input.Key.AltLeft => ImGuiKey.LeftAlt,
            Silk.NET.Input.Key.AltRight => ImGuiKey.RightAlt,
            Silk.NET.Input.Key.SuperLeft => ImGuiKey.LeftSuper,
            Silk.NET.Input.Key.SuperRight => ImGuiKey.RightSuper,
            Silk.NET.Input.Key.CapsLock => ImGuiKey.CapsLock,
            Silk.NET.Input.Key.ScrollLock => ImGuiKey.ScrollLock,
            Silk.NET.Input.Key.NumLock => ImGuiKey.NumLock,

            // Punctuation
            Silk.NET.Input.Key.Minus => ImGuiKey.Minus,
            Silk.NET.Input.Key.Equal => ImGuiKey.Equal,
            Silk.NET.Input.Key.LeftBracket => ImGuiKey.LeftBracket,
            Silk.NET.Input.Key.RightBracket => ImGuiKey.RightBracket,
            Silk.NET.Input.Key.Semicolon => ImGuiKey.Semicolon,
            Silk.NET.Input.Key.Apostrophe => ImGuiKey.Apostrophe,
            Silk.NET.Input.Key.GraveAccent => ImGuiKey.GraveAccent,
            Silk.NET.Input.Key.Comma => ImGuiKey.Comma,
            Silk.NET.Input.Key.Period => ImGuiKey.Period,
            Silk.NET.Input.Key.Slash => ImGuiKey.Slash,
            Silk.NET.Input.Key.BackSlash => ImGuiKey.Backslash,

            // Menu
            Silk.NET.Input.Key.Menu => ImGuiKey.Menu,

            // Numpad
            Silk.NET.Input.Key.Keypad0 => ImGuiKey.Keypad0,
            Silk.NET.Input.Key.Keypad1 => ImGuiKey.Keypad1,
            Silk.NET.Input.Key.Keypad2 => ImGuiKey.Keypad2,
            Silk.NET.Input.Key.Keypad3 => ImGuiKey.Keypad3,
            Silk.NET.Input.Key.Keypad4 => ImGuiKey.Keypad4,
            Silk.NET.Input.Key.Keypad5 => ImGuiKey.Keypad5,
            Silk.NET.Input.Key.Keypad6 => ImGuiKey.Keypad6,
            Silk.NET.Input.Key.Keypad7 => ImGuiKey.Keypad7,
            Silk.NET.Input.Key.Keypad8 => ImGuiKey.Keypad8,
            Silk.NET.Input.Key.Keypad9 => ImGuiKey.Keypad9,
            Silk.NET.Input.Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Silk.NET.Input.Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Silk.NET.Input.Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Silk.NET.Input.Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
            Silk.NET.Input.Key.KeypadAdd => ImGuiKey.KeypadAdd,
            Silk.NET.Input.Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Silk.NET.Input.Key.KeypadEqual => ImGuiKey.KeypadEqual,

            _ => ImGuiKey.None,
        };
    }
}
