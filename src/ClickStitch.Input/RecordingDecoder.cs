namespace ClickStitch.Input;

/// <summary>
/// Turns one hook sample into a button, a schema key, a move, or a wheel notch.
/// Movement is accepted only when the options ask for it. An injected sample is skipped when the caller asks.
/// The work is a lookup, not a document edit.
/// </summary>
internal static class RecordingDecoder
{
    internal const int KeyDown = 0x0100;
    internal const int KeyUp = 0x0101;
    internal const int SysKeyDown = 0x0104;
    internal const int SysKeyUp = 0x0105;
    internal const int MouseMove = 0x0200;
    internal const int LeftDown = 0x0201;
    internal const int LeftUp = 0x0202;
    internal const int RightDown = 0x0204;
    internal const int RightUp = 0x0205;
    internal const int MiddleDown = 0x0207;
    internal const int MiddleUp = 0x0208;
    internal const int MouseWheel = 0x020A;
    internal const int MouseHorizontalWheel = 0x020E;

    internal const uint KeyboardInjected = 0x10;
    internal const uint MouseInjected = 0x01;
    internal const uint MouseInjectedFromLowerIntegrity = 0x02;

    private static readonly Dictionary<uint, InputKey> Keys = CreateKeys();

    internal static bool IsKeyboardInjected(uint flags) => (flags & KeyboardInjected) != 0;

    internal static bool IsMouseInjected(uint flags) =>
        (flags & (MouseInjected | MouseInjectedFromLowerIntegrity)) != 0;

    internal static bool TryDecode(HookSample sample, RecordingOptions options, out RecordedEvent recorded)
    {
        recorded = default;
        if (options.IgnoreInjected && sample.Injected)
        {
            return false;
        }

        switch (sample.Message)
        {
            case MouseMove:
                if (!options.CaptureMovement)
                {
                    return false;
                }

                recorded = RecordedEvent.Move(sample.Timestamp, sample.X, sample.Y);
                return true;
            case MouseWheel:
                if (!options.CaptureWheel || sample.WheelDelta == 0)
                {
                    return false;
                }

                recorded = RecordedEvent.Wheel(sample.Timestamp, sample.WheelDelta);
                return true;
            case MouseHorizontalWheel:
                return false;
            case LeftDown:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Left, true, sample.X, sample.Y);
                return true;
            case LeftUp:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Left, false, sample.X, sample.Y);
                return true;
            case RightDown:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Right, true, sample.X, sample.Y);
                return true;
            case RightUp:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Right, false, sample.X, sample.Y);
                return true;
            case MiddleDown:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Middle, true, sample.X, sample.Y);
                return true;
            case MiddleUp:
                recorded = RecordedEvent.Mouse(sample.Timestamp, MouseButton.Middle, false, sample.X, sample.Y);
                return true;
            case KeyDown:
            case SysKeyDown:
            case KeyUp:
            case SysKeyUp:
                if (!Keys.TryGetValue(sample.VirtualKey, out InputKey key))
                {
                    return false;
                }

                bool down = sample.Message is KeyDown or SysKeyDown;
                recorded = RecordedEvent.KeyPress(sample.Timestamp, key, down);
                return true;
            default:
                return false;
        }
    }

    private static Dictionary<uint, InputKey> CreateKeys()
    {
        var keys = new Dictionary<uint, InputKey>(KeyStroke.SchemaKeyCount);
        foreach (InputKey key in Enum.GetValues<InputKey>())
        {
            keys[InputKeys.Describe(key).VirtualKey] = key;
        }

        return keys;
    }
}
