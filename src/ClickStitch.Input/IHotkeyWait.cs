namespace ClickStitch.Input;

/// <summary>
/// Blocks until a hotkey id arrives, or until <see cref="Stop"/> asks the listener to exit.
/// </summary>
internal interface IHotkeyWait
{
    int? WaitForHotkey();

    void Stop();
}
