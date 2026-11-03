namespace ClickStitch.Input;

/// <summary>
/// What a hotkey does. Emergency stop is its own method so it cannot be routed through a plugin or the start callback.
/// </summary>
internal interface IHotkeyTarget
{
    void Start();

    void PauseOrResume();

    void Stop();

    void EmergencyStop();
}
