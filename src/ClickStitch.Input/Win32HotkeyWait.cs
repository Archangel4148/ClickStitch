using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Pumps the thread's queue for <c>WM_HOTKEY</c>. <see cref="Stop"/> posts <c>WM_QUIT</c>.
/// </summary>
internal sealed class Win32HotkeyWait : IHotkeyWait
{
    private const uint HotkeyMessage = 0x0312;
    private const uint QuitMessage = 0x0012;

    private uint _threadId;

    internal void BindThread() => _threadId = GetCurrentThreadId();

    public int? WaitForHotkey()
    {
        while (GetMessage(out NativeMessage message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.Message == HotkeyMessage)
            {
                return (int)message.WParam;
            }

            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        return null;
    }

    public void Stop()
    {
        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, QuitMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// x64 <c>MSG</c>. The point starts at byte 40. A sequential layout would put it at 36 and overflow <c>GetMessage</c>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct NativeMessage
    {
        [FieldOffset(0)]
        internal IntPtr Window;

        [FieldOffset(8)]
        internal uint Message;

        [FieldOffset(16)]
        internal nuint WParam;

        [FieldOffset(24)]
        internal nint LParam;

        [FieldOffset(32)]
        internal uint Time;

        [FieldOffset(40)]
        internal int X;

        [FieldOffset(44)]
        internal int Y;
    }
}
