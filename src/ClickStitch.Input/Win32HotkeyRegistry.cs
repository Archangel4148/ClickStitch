using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Calls <c>RegisterHotKey</c>. This is not a low-level keyboard hook.
/// </summary>
internal sealed class Win32HotkeyRegistry : IHotkeyRegistry
{
    public bool Register(int id, uint modifiers, uint virtualKey, out int win32Error)
    {
        bool registered = RegisterHotKey(IntPtr.Zero, id, modifiers, virtualKey);
        win32Error = registered ? 0 : Marshal.GetLastWin32Error();
        return registered;
    }

    public bool Unregister(int id, out int win32Error)
    {
        bool removed = UnregisterHotKey(IntPtr.Zero, id);
        win32Error = removed ? 0 : Marshal.GetLastWin32Error();
        return removed;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
