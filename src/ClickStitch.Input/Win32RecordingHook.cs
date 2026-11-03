using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Installs the low-level mouse and keyboard hooks on a thread that pumps messages.
/// The callback copies one sample and returns. It does not unhook. <see cref="Uninstall"/> posts quit and joins.
/// This type is not used by the default test suite.
/// </summary>
internal sealed class Win32RecordingHook : IRecordingHook
{
    private const int MouseHook = 14;
    private const int KeyboardHook = 13;
    private const uint QuitMessage = 0x0012;

    private readonly Lock _gate = new();
    private Thread? _thread;
    private uint _threadId;
    private Action<HookSample>? _onSample;
    private Exception? _startError;
    private volatile bool _quit;

    public bool Install(Action<HookSample> onSample)
    {
        ArgumentNullException.ThrowIfNull(onSample);
        var ready = new ManualResetEventSlim(false);
        lock (_gate)
        {
            if (_thread is not null)
            {
                throw new InvalidOperationException("The recording hook is already installed.");
            }

            _onSample = onSample;
            _quit = false;
            _startError = null;
            _thread = new Thread(() => Run(ready))
            {
                IsBackground = true,
                Name = "ClickStitch.Recording",
            };
            _thread.Start();
        }

        ready.Wait();
        if (_startError is not null || _threadId == 0)
        {
            Join();
        }

        ready.Dispose();
        if (_startError is not null)
        {
            throw new InvalidOperationException("The recording hook failed to start.", _startError);
        }

        return _threadId != 0;
    }

    public void Uninstall()
    {
        _quit = true;
        uint threadId = _threadId;
        if (threadId != 0)
        {
            PostThreadMessage(threadId, QuitMessage, IntPtr.Zero, IntPtr.Zero);
        }

        Join();
    }

    private void Join()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
        }

        if (thread is null || thread == Thread.CurrentThread)
        {
            return;
        }

        thread.Join();
        lock (_gate)
        {
            if (_thread == thread)
            {
                _thread = null;
                _threadId = 0;
                _onSample = null;
            }
        }
    }

    private void Run(ManualResetEventSlim ready)
    {
        IntPtr mouse = IntPtr.Zero;
        IntPtr keyboard = IntPtr.Zero;
        HookProc? mouseProc = null;
        HookProc? keyboardProc = null;
        bool signaled = false;
        try
        {
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            _threadId = GetCurrentThreadId();
            mouseProc = MouseProcedure;
            keyboardProc = KeyboardProcedure;
            mouse = SetWindowsHookEx(MouseHook, mouseProc, IntPtr.Zero, 0);
            keyboard = SetWindowsHookEx(KeyboardHook, keyboardProc, IntPtr.Zero, 0);
            if (mouse == IntPtr.Zero || keyboard == IntPtr.Zero)
            {
                _threadId = 0;
                return;
            }

            signaled = true;
            ready.Set();
            while (!_quit && GetMessage(out NativeMessage message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.Message == QuitMessage)
                {
                    break;
                }

                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception error)
        {
            _startError = error;
            _threadId = 0;
        }
        finally
        {
            if (mouse != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouse);
            }

            if (keyboard != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboard);
            }

            if (!signaled)
            {
                ready.Set();
            }
        }
    }

    private IntPtr MouseProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && lParam != IntPtr.Zero)
        {
            var data = Marshal.PtrToStructure<MouseHookData>(lParam);
            int wheelDelta = (short)(data.MouseData >> 16);
            Offer(new HookSample(
                (int)wParam,
                0,
                RecordingDecoder.IsMouseInjected(data.Flags),
                MonotonicClock.Timestamp,
                data.X,
                data.Y,
                wheelDelta));
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr KeyboardProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && lParam != IntPtr.Zero)
        {
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            Offer(new HookSample((int)wParam, data.VirtualKey, RecordingDecoder.IsKeyboardInjected(data.Flags), MonotonicClock.Timestamp));
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private void Offer(HookSample sample)
    {
        try
        {
            _onSample?.Invoke(sample);
        }
        catch (Exception error)
        {
            _startError = error;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct MouseHookData
    {
        [FieldOffset(0)]
        internal int X;

        [FieldOffset(4)]
        internal int Y;

        // POINT is 8 bytes. mouseData follows, then flags.
        [FieldOffset(8)]
        internal uint MouseData;

        [FieldOffset(12)]
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct KeyboardHookData
    {
        [FieldOffset(0)]
        internal uint VirtualKey;

        [FieldOffset(8)]
        internal uint Flags;
    }

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

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
}
