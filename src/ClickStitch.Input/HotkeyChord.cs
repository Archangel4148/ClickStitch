namespace ClickStitch.Input;

/// <summary>
/// Win32 modifier flags for <c>RegisterHotKey</c>, without <c>MOD_NOREPEAT</c>.
/// The registrar always adds no-repeat so a held chord does not fire again.
/// </summary>
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}

/// <summary>
/// One global chord. The key is the virtual key, not a modifier.
/// </summary>
internal readonly record struct HotkeyChord(InputKey Key, HotkeyModifiers Modifiers)
{
    internal const uint NoRepeat = 0x4000;

    internal uint NativeModifiers => (uint)Modifiers | NoRepeat;

    internal ushort VirtualKey => InputKeys.Describe(Key).VirtualKey;
}
