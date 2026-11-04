namespace ClickStitch.Input;

/// <summary>A global shortcut that toggles the clicker. Parsing does not register the shortcut.</summary>
public readonly record struct ToggleBinding(string Text, uint Modifiers, ushort VirtualKey);

public static class ToggleChord
{
    public static bool TryParse(string? text, out ToggleBinding binding, out string problem)
    {
        binding = default;
        problem = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "Choose a shortcut.";
            return false;
        }

        string[] parts = text.Trim().Split('+');
        if (parts.Length is < 1 or > 5)
        {
            problem = "Use a function key, or Ctrl, Alt, Shift, or Win with a key.";
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        for (int index = 0; index < parts.Length - 1; index++)
        {
            if (!TryModifier(parts[index], out HotkeyModifiers flag) || modifiers.HasFlag(flag))
            {
                problem = "Use each of Ctrl, Alt, Shift, and Win only once.";
                return false;
            }

            modifiers |= flag;
        }

        if (!Enum.TryParse(parts[^1], ignoreCase: false, out InputKey key) || !Enum.IsDefined(key) || IsModifier(key))
        {
            problem = "The shortcut needs a key that is not Ctrl, Alt, Shift, or Windows.";
            return false;
        }

        if (modifiers == HotkeyModifiers.None && !IsFunction(key))
        {
            problem = "Use a function key, or add Ctrl, Alt, or Shift.";
            return false;
        }

        binding = new ToggleBinding(Format(modifiers, key), (uint)modifiers, InputKeys.Describe(key).VirtualKey);
        return true;
    }

    private static bool TryModifier(string text, out HotkeyModifiers flag)
    {
        switch (text)
        {
            case "Ctrl":
                flag = HotkeyModifiers.Control;
                return true;
            case "Alt":
                flag = HotkeyModifiers.Alt;
                return true;
            case "Shift":
                flag = HotkeyModifiers.Shift;
                return true;
            case "Win":
                flag = HotkeyModifiers.Windows;
                return true;
            default:
                flag = HotkeyModifiers.None;
                return false;
        }
    }

    private static bool IsModifier(InputKey key) =>
        key is InputKey.Shift or InputKey.Control or InputKey.Alt or InputKey.LeftWindows or InputKey.RightWindows;

    private static bool IsFunction(InputKey key) => key is >= InputKey.F1 and <= InputKey.F24;

    private static string Format(HotkeyModifiers modifiers, InputKey key)
    {
        var names = new List<string>(5);
        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            names.Add("Ctrl");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            names.Add("Alt");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            names.Add("Shift");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            names.Add("Win");
        }

        names.Add(key.ToString());
        return string.Join('+', names);
    }
}
