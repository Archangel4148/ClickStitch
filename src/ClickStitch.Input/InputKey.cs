namespace ClickStitch.Input;

/// <summary>
/// A key the macro schema can name.
/// <see cref="A"/> through <see cref="Z"/>, <see cref="Digit0"/> through <see cref="Digit9"/>,
/// and <see cref="F1"/> through <see cref="F24"/> are contiguous ranges. Do not insert members inside them.
/// </summary>
internal enum InputKey
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,
    Shift, Control, Alt, LeftWindows, RightWindows,
    Space, Enter, Tab, Escape, Backspace, Delete, Insert,
    Home, End, PageUp, PageDown,
    Left, Right, Up, Down,
    Comma, Period, Minus, Equal, Semicolon, Slash, Backslash, Apostrophe, LeftBracket, RightBracket,
}

/// <summary>
/// Virtual key and whether <c>SendInput</c> must set the extended-key flag.
/// </summary>
internal readonly record struct KeyStroke(ushort VirtualKey, bool Extended)
{
    internal const int SchemaKeyCount = 90;
}

internal static class InputKeys
{
    internal static KeyStroke Describe(InputKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "The key is not in the macro schema.");
        }

        int ordinal = (int)key;
        if (ordinal >= (int)InputKey.A && ordinal <= (int)InputKey.Z)
        {
            return new KeyStroke((ushort)(0x41 + ordinal - (int)InputKey.A), false);
        }

        if (ordinal >= (int)InputKey.Digit0 && ordinal <= (int)InputKey.Digit9)
        {
            return new KeyStroke((ushort)(0x30 + ordinal - (int)InputKey.Digit0), false);
        }

        if (ordinal >= (int)InputKey.F1 && ordinal <= (int)InputKey.F24)
        {
            return new KeyStroke((ushort)(0x70 + ordinal - (int)InputKey.F1), false);
        }

        return key switch
        {
            InputKey.Shift => new(0x10, false),
            InputKey.Control => new(0x11, false),
            InputKey.Alt => new(0x12, false),
            InputKey.LeftWindows => new(0x5B, true),
            InputKey.RightWindows => new(0x5C, true),
            InputKey.Space => new(0x20, false),
            InputKey.Enter => new(0x0D, false),
            InputKey.Tab => new(0x09, false),
            InputKey.Escape => new(0x1B, false),
            InputKey.Backspace => new(0x08, false),
            InputKey.Delete => new(0x2E, true),
            InputKey.Insert => new(0x2D, true),
            InputKey.Home => new(0x24, true),
            InputKey.End => new(0x23, true),
            InputKey.PageUp => new(0x21, true),
            InputKey.PageDown => new(0x22, true),
            InputKey.Left => new(0x25, true),
            InputKey.Up => new(0x26, true),
            InputKey.Right => new(0x27, true),
            InputKey.Down => new(0x28, true),
            InputKey.Comma => new(0xBC, false),
            InputKey.Period => new(0xBE, false),
            InputKey.Minus => new(0xBD, false),
            InputKey.Equal => new(0xBB, false),
            InputKey.Semicolon => new(0xBA, false),
            InputKey.Slash => new(0xBF, false),
            InputKey.Backslash => new(0xDC, false),
            InputKey.Apostrophe => new(0xDE, false),
            InputKey.LeftBracket => new(0xDB, false),
            InputKey.RightBracket => new(0xDD, false),
            _ => throw new InvalidOperationException($"No virtual-key mapping for {key}."),
        };
    }
}
