namespace ClickStitch.Input;

/// <summary>
/// The four global shortcuts. Two roles cannot share a chord.
/// Proposed defaults use Ctrl+Alt+Shift with F9, F10, F11, and F12.
/// F12 is the emergency stop already named in the architecture.
/// </summary>
internal sealed class HotkeyBindings
{
    internal HotkeyBindings(HotkeyChord start, HotkeyChord pauseResume, HotkeyChord stop, HotkeyChord emergencyStop)
    {
        Start = Validate(start);
        PauseResume = Validate(pauseResume);
        Stop = Validate(stop);
        EmergencyStop = Validate(emergencyStop);
        if (SharesChord())
        {
            throw new ArgumentException("Two hotkeys use the same chord.");
        }
    }

    internal static HotkeyBindings ProposedDefaults { get; } = new(
        new HotkeyChord(InputKey.F9, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift),
        new HotkeyChord(InputKey.F10, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift),
        new HotkeyChord(InputKey.F11, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift),
        new HotkeyChord(InputKey.F12, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift));

    internal HotkeyChord Start { get; }

    internal HotkeyChord PauseResume { get; }

    internal HotkeyChord Stop { get; }

    internal HotkeyChord EmergencyStop { get; }

    internal HotkeyChord ChordFor(HotkeyRole role) => role switch
    {
        HotkeyRole.Start => Start,
        HotkeyRole.PauseResume => PauseResume,
        HotkeyRole.Stop => Stop,
        HotkeyRole.EmergencyStop => EmergencyStop,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    internal static HotkeyChord Validate(HotkeyChord chord)
    {
        if (chord.Modifiers == HotkeyModifiers.None)
        {
            throw new ArgumentException("A global hotkey needs a modifier so it does not swallow an ordinary key.");
        }

        if (chord.Key is InputKey.Shift or InputKey.Control or InputKey.Alt or InputKey.LeftWindows or InputKey.RightWindows)
        {
            throw new ArgumentException("The hotkey's main key cannot be a modifier.");
        }

        _ = chord.VirtualKey;
        return chord;
    }

    private bool SharesChord()
    {
        HotkeyChord[] chords = [Start, PauseResume, Stop, EmergencyStop];
        for (int left = 0; left < chords.Length; left++)
        {
            for (int right = left + 1; right < chords.Length; right++)
            {
                if (chords[left] == chords[right])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
