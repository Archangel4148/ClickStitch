namespace ClickStitch.Input;

/// <summary>
/// <c>RegisterHotKey</c> and <c>UnregisterHotKey</c>. The window handle is null so the message
/// goes to the thread that registered the chord. Tests supply a stand-in.
/// </summary>
internal interface IHotkeyRegistry
{
    bool Register(int id, uint modifiers, uint virtualKey, out int win32Error);

    bool Unregister(int id, out int win32Error);
}
