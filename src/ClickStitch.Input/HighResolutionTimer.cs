using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// One high-resolution waitable timer. Windows 11 supports <c>CREATE_WAITABLE_TIMER_HIGH_RESOLUTION</c>.
/// This type does not call <c>timeBeginPeriod</c>.
/// </summary>
internal sealed class HighResolutionTimer : IDisposable
{
    private const uint CreateHighResolution = 0x00000002;
    private const uint TimerAllAccess = 0x001F0003;

    private IntPtr _handle;

    internal HighResolutionTimer()
    {
        _handle = CreateWaitableTimerEx(IntPtr.Zero, null, CreateHighResolution, TimerAllAccess);
        if (_handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWaitableTimerEx could not create a high-resolution timer.");
        }
    }

    internal IntPtr Handle => _handle;

    internal void Arm(long relativeDueTime100Ns)
    {
        ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
        if (relativeDueTime100Ns >= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeDueTime100Ns), "The due time is relative and must be negative.");
        }

        if (!SetWaitableTimer(_handle, ref relativeDueTime100Ns, 0, IntPtr.Zero, IntPtr.Zero, false))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWaitableTimer failed.");
        }
    }

    internal void Cancel()
    {
        if (_handle != IntPtr.Zero)
        {
            CancelWaitableTimer(_handle);
        }
    }

    public void Dispose()
    {
        IntPtr handle = _handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _handle = IntPtr.Zero;
        CancelWaitableTimer(handle);
        CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWaitableTimerExW")]
    private static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(
        IntPtr timer,
        ref long dueTime,
        int period,
        IntPtr completion,
        IntPtr argument,
        bool resume);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelWaitableTimer(IntPtr timer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
