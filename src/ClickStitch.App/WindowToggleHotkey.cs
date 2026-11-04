using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClickStitch.Input;

namespace ClickStitch;

/// <summary>One global shortcut on this window. Tests do not construct it, so they do not register a shortcut.</summary>
public sealed class WindowToggleHotkey : IDisposable
{
    private const int Id = 0x4353;
    private const uint NoRepeat = 0x4000;
    private const int HotkeyMessage = 0x0312;

    private readonly Window _window;
    private readonly Action _toggle;
    private HwndSource? _source;
    private bool _registered;
    private bool _disposed;

    public WindowToggleHotkey(Window window, Action toggle)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(toggle);
        _window = window;
        _toggle = toggle;
    }

    public string? Bound { get; private set; }

    public void Attach()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_source is not null)
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    public bool TryBind(string text, out string problem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ToggleChord.TryParse(text, out ToggleBinding binding, out problem))
        {
            return false;
        }

        IntPtr handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            problem = "The window is not ready for a shortcut yet.";
            return false;
        }

        Clear();
        if (!RegisterHotKey(handle, Id, binding.Modifiers | NoRepeat, binding.VirtualKey))
        {
            problem = "That shortcut is already used. Try another key.";
            return false;
        }

        _registered = true;
        Bound = binding.Text;
        return true;
    }

    public void Clear()
    {
        if (_registered)
        {
            UnregisterHotKey(new WindowInteropHelper(_window).Handle, Id);
            _registered = false;
        }

        Bound = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyMessage && wParam == (IntPtr)Id)
        {
            handled = true;
            _window.Dispatcher.BeginInvoke(_toggle);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
