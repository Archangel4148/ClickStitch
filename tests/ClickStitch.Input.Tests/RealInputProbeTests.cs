using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace ClickStitch.Input.Tests;

/// <summary>
/// Sends real input only into a window this test created.
/// A click, key, or wheel is sent only while that window is in front and the pointer is inside it.
/// The keys are F13 and F14, which do not type a character or open a menu.
/// Run with <c>make manual</c>. The default suite does not run this test.
/// </summary>
public sealed class RealInputProbeTests
{
    private readonly ITestOutputHelper _output;

    public RealInputProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Manual")]
    public void Click_key_wheel_and_release_stay_inside_the_probe_window()
    {
        if (Environment.GetEnvironmentVariable("CLICKSTITCH_MANUAL_INPUT") != "1")
        {
            throw new InvalidOperationException(
                "Refusing to inject desktop input. Run make manual.");
        }

        ProbeWindow.RequirePerMonitorAwareness();
        var manager = InputStateManager.CreateForWindows();
        var primitives = InputPrimitives.CreateForWindows();
        using var probe = ProbeWindow.Open();
        using var worker = new ExecutionWorker(manager, DeadlineWait.CreateForWindows());
        ProbePoint saved = probe.ReadCursor();
        bool moved = false;
        try
        {
            probe.Arm(_output);
            ProbePoint target = probe.ClientCenter();
            VirtualScreen screen = probe.ReadVirtualScreen();
            if (!PointerCoordinates.TryToAbsolute(screen, target.X, target.Y, out _, out _))
            {
                throw new InvalidOperationException("The probe center is outside the virtual screen. No input was sent.");
            }

            probe.PlaceCursor(target);
            probe.RequireSafe("before the pointer move");
            Accept(primitives.MoveTo(screen, target.X, target.Y));
            moved = true;
            probe.RequireLanded(target, "The pointer move did not land in the probe. No button or key was sent.");
            probe.RequireSafe("before the click");

            Accept(primitives.Click(MouseButton.Left));
            probe.PumpUntil("the click", () => probe.LeftDowns == 1 && probe.LeftUps == 1);
            probe.RequireClickNearCenter();
            _output.WriteLine($"Click landed at client {probe.ClickX},{probe.ClickY}.");

            probe.RequireSafe("before the key");
            OwnershipResult down = manager.KeyDown(InputKey.F13);
            Accept(down);
            OwnershipResult up = manager.KeyUp(InputKey.F13);
            Accept(up);
            probe.PumpUntil("F13", () => probe.F13Downs == 1 && probe.F13Ups == 1);
            Assert.Empty(manager.Snapshot());

            probe.RequireSafe("before the wheel");
            Accept(primitives.Wheel(1));
            probe.PumpUntil("the wheel", () => probe.WheelNotches == 1);

            probe.RequireSafe("before the held key");
            worker.Start(
            [
                WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.F14)),
                WorkerActions.Wait(5_000),
            ]);
            probe.PumpUntil("F14 down", () => probe.F14Downs == 1);
            worker.Stop();
            probe.PumpUntil("F14 up", () => probe.F14Ups == 1);
            Assert.Empty(manager.Snapshot());
            Assert.Equal(0, probe.RightDowns);
            Assert.Equal(0, probe.SystemKeyDowns);

            probe.Title("ClickStitch probe finished. Pointer restored. Inputs released.");
            probe.Pump(TimeSpan.FromMilliseconds(600));
            _output.WriteLine("Click, F13, wheel, and F14 release were received by the probe window.");
        }
        finally
        {
            if (worker.State is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
            {
                worker.Stop();
            }

            manager.ReleaseAllOwnedInputs();
            probe.PlaceCursor(saved);
            if (moved)
            {
                _output.WriteLine($"Pointer restored to {saved.X},{saved.Y}.");
            }
        }
    }

    private static void Accept(PrimitiveResult result)
    {
        if (result.Delivery != PrimitiveDelivery.Sent || !result.Submission.IsFull)
        {
            throw new InvalidOperationException(
                $"Windows did not accept the batch ({result.Delivery}, error {result.Submission.Win32Error}).");
        }
    }

    private static void Accept(OwnershipResult result)
    {
        if (result.Outcome != OwnershipOutcome.Sent || !result.Submission.IsFull)
        {
            throw new InvalidOperationException(
                $"Windows did not accept the owned batch ({result.Outcome}, error {result.Submission.Win32Error}).");
        }
    }
}

internal sealed class ProbeWindow : IDisposable
{
    private const int LandingTolerance = 8;
    private const int PerMonitorAwareV2 = -4;

    private static readonly ProbeNative.WndProc Procedure = WindowProcedure;
    private static ProbeWindow? _active;

    private readonly IntPtr _handle;
    private bool _disposed;

    private ProbeWindow(IntPtr handle) => _handle = handle;

    internal int LeftDowns { get; private set; }

    internal int LeftUps { get; private set; }

    internal int RightDowns { get; private set; }

    internal int SystemKeyDowns { get; private set; }

    internal int F13Downs { get; private set; }

    internal int F13Ups { get; private set; }

    internal int F14Downs { get; private set; }

    internal int F14Ups { get; private set; }

    internal int WheelNotches { get; private set; }

    internal int ClickX { get; private set; }

    internal int ClickY { get; private set; }

    internal static void RequirePerMonitorAwareness()
    {
        bool set = ProbeNative.SetProcessDpiAwarenessContext(new IntPtr(PerMonitorAwareV2));
        if (!set && Marshal.GetLastWin32Error() != 5)
        {
            throw new InvalidOperationException(
                "Refusing to inject input because per-monitor DPI awareness could not be set. A click could land in the wrong place.");
        }

        if (!ProbeNative.AreDpiAwarenessContextsEqual(
                ProbeNative.GetThreadDpiAwarenessContext(),
                new IntPtr(PerMonitorAwareV2)))
        {
            throw new InvalidOperationException(
                "Refusing to inject input because this process is not per-monitor DPI aware.");
        }
    }

    internal static ProbeWindow Open()
    {
        IntPtr instance = ProbeNative.GetModuleHandle(null);
        var info = new ProbeNative.WindowClass
        {
            Size = (uint)Marshal.SizeOf<ProbeNative.WindowClass>(),
            Procedure = Marshal.GetFunctionPointerForDelegate(Procedure),
            Instance = instance,
            Background = new IntPtr(6),
            ClassName = "ClickStitchProbe",
        };
        ushort atom = ProbeNative.RegisterClass(ref info);
        if (atom == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            throw new InvalidOperationException($"The probe window class was not registered ({Marshal.GetLastWin32Error()}).");
        }

        ProbeNative.Rect work = default;
        if (!ProbeNative.SystemParametersInfo(48, 0, ref work, 0))
        {
            throw new InvalidOperationException("The work area could not be read. No input was sent.");
        }

        IntPtr handle = ProbeNative.CreateWindow(
            0x00000008,
            "ClickStitchProbe",
            "ClickStitch probe. Do not use the mouse until the pointer returns.",
            0x00C00000 | 0x00080000 | 0x10000000,
            work.Left + 80,
            work.Top + 80,
            520,
            360,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The probe window was not created ({Marshal.GetLastWin32Error()}). No input was sent.");
        }

        var probe = new ProbeWindow(handle);
        _active = probe;
        return probe;
    }

    internal void Arm(ITestOutputHelper output)
    {
        ProbeNative.ShowWindow(_handle, 5);
        ProbeNative.UpdateWindow(_handle);
        ProbeNative.SetWindowPos(_handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        TryForeground();
        var waited = Stopwatch.StartNew();
        while (ProbeNative.GetForegroundWindow() != _handle)
        {
            Pump(TimeSpan.FromMilliseconds(15));
            TryForeground();
            if (waited.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new InvalidOperationException(
                    "The probe window did not come to the front. No click, key, or wheel was sent.");
            }
        }

        ResetCounts();
        output.WriteLine("Probe window is in front. Injection stays inside it.");
    }

    internal ProbePoint ReadCursor()
    {
        if (!ProbeNative.GetCursorPos(out ProbeNative.Point point))
        {
            throw new InvalidOperationException("The pointer position could not be read.");
        }

        return new ProbePoint(point.X, point.Y);
    }

    internal void PlaceCursor(ProbePoint point)
    {
        if (!ProbeNative.SetCursorPos(point.X, point.Y))
        {
            throw new InvalidOperationException("The pointer could not be restored.");
        }
    }

    internal ProbePoint ClientCenter()
    {
        if (!ProbeNative.GetClientRect(_handle, out ProbeNative.Rect client))
        {
            throw new InvalidOperationException("The probe client rectangle could not be read.");
        }

        var point = new ProbeNative.Point { X = client.Right / 2, Y = client.Bottom / 2 };
        if (!ProbeNative.ClientToScreen(_handle, ref point))
        {
            throw new InvalidOperationException("The probe center could not be converted to screen pixels.");
        }

        return new ProbePoint(point.X, point.Y);
    }

    internal VirtualScreen ReadVirtualScreen() => new(
        ProbeNative.GetSystemMetrics(76),
        ProbeNative.GetSystemMetrics(77),
        ProbeNative.GetSystemMetrics(78),
        ProbeNative.GetSystemMetrics(79));

    internal void RequireLanded(ProbePoint expected, string message)
    {
        ProbePoint now = ReadCursor();
        if (Math.Abs(now.X - expected.X) > LandingTolerance || Math.Abs(now.Y - expected.Y) > LandingTolerance)
        {
            throw new InvalidOperationException($"{message} Pointer is at {now.X},{now.Y}; expected {expected.X},{expected.Y}.");
        }
    }

    internal void RequireSafe(string moment)
    {
        if (!ProbeNative.IsWindow(_handle) || ProbeNative.GetForegroundWindow() != _handle)
        {
            throw new InvalidOperationException($"The probe is not the foreground window {moment}. Stopping before the next input.");
        }

        ProbePoint cursor = ReadCursor();
        if (!ProbeNative.GetClientRect(_handle, out ProbeNative.Rect client))
        {
            throw new InvalidOperationException($"The probe client rectangle could not be read {moment}.");
        }

        var origin = new ProbeNative.Point();
        if (!ProbeNative.ClientToScreen(_handle, ref origin))
        {
            throw new InvalidOperationException($"The probe origin could not be read {moment}.");
        }

        bool inside = cursor.X >= origin.X && cursor.Y >= origin.Y
            && cursor.X < origin.X + client.Right && cursor.Y < origin.Y + client.Bottom;
        var hitPoint = new ProbeNative.Point { X = cursor.X, Y = cursor.Y };
        IntPtr hit = ProbeNative.WindowFromPoint(hitPoint);
        if (!inside || hit != _handle)
        {
            throw new InvalidOperationException(
                $"The pointer is not inside the probe {moment} (at {cursor.X},{cursor.Y}). Stopping before the next input.");
        }
    }

    internal void RequireClickNearCenter()
    {
        if (!ProbeNative.GetClientRect(_handle, out ProbeNative.Rect client))
        {
            throw new InvalidOperationException("The probe client rectangle could not be read after the click.");
        }

        int centerX = client.Right / 2;
        int centerY = client.Bottom / 2;
        if (Math.Abs(ClickX - centerX) > LandingTolerance || Math.Abs(ClickY - centerY) > LandingTolerance)
        {
            throw new InvalidOperationException(
                $"The click landed at client {ClickX},{ClickY}, away from {centerX},{centerY}.");
        }
    }

    internal void Title(string text) => ProbeNative.SetWindowText(_handle, text);

    internal void PumpUntil(string what, Func<bool> done)
    {
        var waited = Stopwatch.StartNew();
        while (!done())
        {
            if (waited.Elapsed > TimeSpan.FromSeconds(2))
            {
                throw new TimeoutException($"The probe did not receive {what}.");
            }

            Pump(TimeSpan.FromMilliseconds(15));
        }
    }

    internal void Pump(TimeSpan slice)
    {
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < slice)
        {
            while (ProbeNative.PeekMessage(out ProbeNative.Message message, IntPtr.Zero, 0, 0, 1))
            {
                ProbeNative.TranslateMessage(ref message);
                ProbeNative.DispatchMessage(ref message);
            }

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_active == this)
        {
            _active = null;
        }

        if (_handle != IntPtr.Zero)
        {
            ProbeNative.DestroyWindow(_handle);
        }
    }

    private void ResetCounts()
    {
        LeftDowns = 0;
        LeftUps = 0;
        RightDowns = 0;
        SystemKeyDowns = 0;
        F13Downs = 0;
        F13Ups = 0;
        F14Downs = 0;
        F14Ups = 0;
        WheelNotches = 0;
        ClickX = 0;
        ClickY = 0;
    }

    private void TryForeground()
    {
        IntPtr foreground = ProbeNative.GetForegroundWindow();
        uint foregroundThread = ProbeNative.GetWindowThreadProcessId(foreground, out _);
        uint thisThread = ProbeNative.GetCurrentThreadId();
        bool attached = false;
        if (foregroundThread != 0 && foregroundThread != thisThread)
        {
            attached = ProbeNative.AttachThreadInput(foregroundThread, thisThread, true);
        }

        try
        {
            ProbeNative.SetForegroundWindow(_handle);
            ProbeNative.BringWindowToTop(_handle);
        }
        finally
        {
            if (attached)
            {
                ProbeNative.AttachThreadInput(foregroundThread, thisThread, false);
            }
        }
    }

    private static IntPtr WindowProcedure(IntPtr window, uint message, nuint wParam, nint lParam)
    {
        ProbeWindow? probe = _active;
        if (probe is null || probe._handle != window && probe._handle != IntPtr.Zero)
        {
            return ProbeNative.DefWindowProc(window, message, wParam, lParam);
        }

        switch (message)
        {
            case 0x0201:
                probe.LeftDowns++;
                probe.ClickX = SignedLow(lParam);
                probe.ClickY = SignedHigh(lParam);
                break;
            case 0x0202:
                probe.LeftUps++;
                break;
            case 0x0204:
                probe.RightDowns++;
                break;
            case 0x0104:
                probe.SystemKeyDowns++;
                break;
            case 0x0100:
                CountKey(probe, (int)(wParam & 0xFFFF), down: true);
                break;
            case 0x0101:
                CountKey(probe, (int)(wParam & 0xFFFF), down: false);
                break;
            case 0x020A:
                probe.WheelNotches += SignedHigh((nint)(wParam & 0xFFFFFFFF)) / 120;
                break;
        }

        return ProbeNative.DefWindowProc(window, message, wParam, lParam);
    }

    private static void CountKey(ProbeWindow probe, int virtualKey, bool down)
    {
        if (virtualKey == 0x7C)
        {
            if (down)
            {
                probe.F13Downs++;
            }
            else
            {
                probe.F13Ups++;
            }
        }
        else if (virtualKey == 0x7D)
        {
            if (down)
            {
                probe.F14Downs++;
            }
            else
            {
                probe.F14Ups++;
            }
        }
    }

    private static int SignedLow(nint value) => (short)(value & 0xFFFF);

    private static int SignedHigh(nint value) => (short)((value >> 16) & 0xFFFF);
}

internal readonly record struct ProbePoint(int X, int Y);

internal static class ProbeNative
{
    internal delegate IntPtr WndProc(IntPtr window, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        internal uint Size;
        internal uint Style;
        internal IntPtr Procedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal IntPtr Instance;
        internal IntPtr Icon;
        internal IntPtr Cursor;
        internal IntPtr Background;
        internal string? MenuName;
        internal string ClassName;
        internal IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct Message
    {
        [FieldOffset(0)]
        internal IntPtr Window;

        [FieldOffset(8)]
        internal uint Id;

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

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    internal static extern bool AreDpiAwarenessContextsEqual(IntPtr left, IntPtr right);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "RegisterClassExW")]
    internal static extern ushort RegisterClass(ref WindowClass info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateWindowExW")]
    internal static extern IntPtr CreateWindow(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll")]
    internal static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    internal static extern bool UpdateWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool SetWindowText(IntPtr window, string text);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachFlag);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    internal static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    internal static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    internal static extern bool SystemParametersInfo(uint action, uint param, ref Rect rect, uint winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PeekMessageW")]
    internal static extern bool PeekMessage(out Message message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    internal static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    internal static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? module);
}
