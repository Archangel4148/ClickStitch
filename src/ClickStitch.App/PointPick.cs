using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClickStitch;

/// <summary>
/// Covers each monitor and records one physical-pixel click. The click stays on the cover, so it does not press whatever is underneath.
/// </summary>
internal static class PointPick
{
    internal static bool IsOpen { get; private set; }

    internal static bool TryPick(Window owner, out int x, out int y)
    {
        x = 0;
        y = 0;
        IsOpen = true;
        try
        {
            return TryPickCore(owner, out x, out y);
        }
        finally
        {
            IsOpen = false;
        }
    }

    private static bool TryPickCore(Window owner, out int x, out int y)
    {
        x = 0;
        y = 0;
        List<MonitorRect> monitors = ReadMonitors();
        if (monitors.Count == 0)
        {
            return false;
        }

        var session = new Session();
        var windows = new List<PickWindow>();
        foreach (MonitorRect monitor in monitors)
        {
            windows.Add(new PickWindow(monitor, session));
        }

        session.Windows = windows;
        PickWindow dialog = windows[0];
        if (TryOwnerCenter(owner, out int ownerX, out int ownerY))
        {
            foreach (PickWindow window in windows)
            {
                if (window.Contains(ownerX, ownerY))
                {
                    dialog = window;
                    break;
                }
            }
        }

        var frame = new DispatcherFrame();
        session.Ended = () => frame.Continue = false;
        foreach (PickWindow window in windows)
        {
            window.Show();
        }

        dialog.Activate();
        Dispatcher.PushFrame(frame);
        session.CloseAll();
        if (!session.Picked)
        {
            return false;
        }

        x = session.X;
        y = session.Y;
        return true;
    }

    private static List<MonitorRect> ReadMonitors()
    {
        var found = new List<MonitorRect>();
        MonitorProc proc = (IntPtr monitor, IntPtr hdc, IntPtr rectPointer, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            RectNative rect = info.Monitor;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width > 1 && height > 1)
            {
                found.Add(new MonitorRect(rect.Left, rect.Top, width, height));
            }

            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        if (found.Count > 0)
        {
            return found;
        }

        int left = GetSystemMetrics(76);
        int top = GetSystemMetrics(77);
        int width = GetSystemMetrics(78);
        int height = GetSystemMetrics(79);
        if (width > 1 && height > 1)
        {
            found.Add(new MonitorRect(left, top, width, height));
        }

        return found;
    }

    private static bool TryOwnerCenter(Window owner, out int x, out int y)
    {
        x = 0;
        y = 0;
        IntPtr handle = new WindowInteropHelper(owner).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out RectNative rect))
        {
            return false;
        }

        x = rect.Left + ((rect.Right - rect.Left) / 2);
        y = rect.Top + ((rect.Bottom - rect.Top) / 2);
        return true;
    }

    private sealed class Session
    {
        internal List<PickWindow> Windows { get; set; } = [];

        internal bool Picked { get; private set; }

        internal int X { get; private set; }

        internal int Y { get; private set; }

        private bool _closing;

        internal Action? Ended { get; set; }

        internal void Choose(int x, int y)
        {
            if (_closing)
            {
                return;
            }

            X = x;
            Y = y;
            Picked = true;
            End();
        }

        internal void Cancel()
        {
            if (_closing)
            {
                return;
            }

            Picked = false;
            End();
        }

        internal void CloseAll() => End();

        private void End()
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            Ended?.Invoke();
            foreach (PickWindow window in Windows)
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        }
    }

    private sealed class PickWindow : Window
    {
        private readonly MonitorRect _monitor;
        private readonly Session _session;

        internal PickWindow(MonitorRect monitor, Session session)
        {
            _monitor = monitor;
            _session = session;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            var dim = new SolidColorBrush(Color.FromArgb(0x55, 0x02, 0x07, 0x18));
            dim.Freeze();
            Background = dim;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Cursor = Cursors.Cross;
            WindowStartupLocation = WindowStartupLocation.Manual;
            var hint = new TextBlock
            {
                Text = "Click the spot. Escape cancels.",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            hint.SetResourceReference(ForegroundProperty, "InkBrush");
            var card = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                BorderThickness = new Thickness(1, 1, 1, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 28, 0, 0),
                IsHitTestVisible = false,
                Child = hint,
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            var root = new Grid { Background = Background };
            root.Children.Add(card);
            Content = root;
            SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (HwndSource.FromHwnd(hwnd) is HwndSource source)
                {
                    source.AddHook(KeepOnMonitor);
                }

                Place();
            };
            ContentRendered += (_, _) => Place();
        }

        internal bool Contains(int x, int y) =>
            x >= _monitor.Left && x < _monitor.Left + _monitor.Width && y >= _monitor.Top && y < _monitor.Top + _monitor.Height;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (GetCursorPos(out PointNative point))
            {
                _session.Choose(point.X, point.Y);
            }

            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            _session.Cancel();
            e.Handled = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _session.Cancel();
                e.Handled = true;
            }
        }

        private void Place()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            IntPtr monitor = MonitorFromPoint(new PointNative { X = _monitor.Left + 1, Y = _monitor.Top + 1 }, 2);
            uint dpiX = 96;
            uint dpiY = 96;
            if (monitor != IntPtr.Zero)
            {
                GetDpiForMonitor(monitor, 0, out dpiX, out dpiY);
            }

            if (dpiX < 1)
            {
                dpiX = 96;
            }

            if (dpiY < 1)
            {
                dpiY = 96;
            }

            Left = _monitor.Left * 96d / dpiX;
            Top = _monitor.Top * 96d / dpiY;
            Width = Math.Max(1, _monitor.Width * 96d / dpiX);
            Height = Math.Max(1, _monitor.Height * 96d / dpiY);
            SetWindowPos(handle, new IntPtr(-1), _monitor.Left, _monitor.Top, _monitor.Width, _monitor.Height, 0x0040);
        }

        private IntPtr KeepOnMonitor(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x0046)
            {
                return IntPtr.Zero;
            }

            var pos = Marshal.PtrToStructure<WindowPos>(lParam);
            pos.X = _monitor.Left;
            pos.Y = _monitor.Top;
            pos.Width = _monitor.Width;
            pos.Height = _monitor.Height;
            pos.Flags &= ~3u;
            Marshal.StructureToPtr(pos, lParam, false);
            return IntPtr.Zero;
        }
    }

    private readonly record struct MonitorRect(int Left, int Top, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        internal int X;
        internal int Y;
    }

    private delegate bool MonitorProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        internal int Size;
        internal RectNative Monitor;
        internal RectNative Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        internal IntPtr Handle;
        internal IntPtr InsertAfter;
        internal int X;
        internal int Y;
        internal int Width;
        internal int Height;
        internal uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorProc proc, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(PointNative point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out RectNative rect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointNative point);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
