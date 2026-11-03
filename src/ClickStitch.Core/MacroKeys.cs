namespace ClickStitch.Core;

/// <summary>
/// Version 1 key names. Numpad keys are not in this list.
/// </summary>
public static class MacroKeys
{
    /// <summary>The schema key names, in schema order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "Digit0", "Digit1", "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24",
        "Shift", "Control", "Alt", "LeftWindows", "RightWindows",
        "Space", "Enter", "Tab", "Escape", "Backspace", "Delete", "Insert",
        "Home", "End", "PageUp", "PageDown",
        "Left", "Right", "Up", "Down",
        "Comma", "Period", "Minus", "Equal", "Semicolon", "Slash", "Backslash", "Apostrophe", "LeftBracket", "RightBracket",
    ];

    private static readonly HashSet<string> Known = new(All, StringComparer.Ordinal);

    /// <summary>True when <paramref name="key"/> is one of the version 1 names, case-sensitive.</summary>
    public static bool IsKnown(string key) => key is not null && Known.Contains(key);
}
