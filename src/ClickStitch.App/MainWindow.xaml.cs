using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClickStitch.Core;
using ClickStitch.Input;
using ClickStitch.Plugins;

namespace ClickStitch;

/// <summary>
/// Shell. Click is the first page. Macros, record, plugins, and settings are the other tabs. The editor does not send input.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DesktopTryout _tryout;
    private readonly DispatcherTimer _timer;
    private readonly List<StepRow> _rows = [];
    private MacroEditor _editor = MacroEditor.Create();
    private string? _savedPath;
    private string _page = "click";
    private bool _filling;
    private bool _discardConfirmed;
    private PendingDiscard _pending;
    private string? _pendingPath;
    private string? _libraryPath;

    private enum PendingDiscard
    {
        None,
        Close,
        Replace,
        Open,
    }

    public MainWindow()
    {
        InitializeComponent();
        Title = "ClickStitch";
        _tryout = DesktopTryout.Create();
        if (Application.Current is App app)
        {
            app.PluginRuntime.ReleaseOwnedInputs = ReleaseAfterPluginFailure;
            NotePluginFaults(app.PluginRuntime.Faults);
            FillSettings(app.Settings);
        }

        HomeFolder.Text = "Macros are saved on this PC in " + MacroStore.DefaultDirectory() + ".";
        try
        {
            Directory.CreateDirectory(MacroStore.DefaultDirectory());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            HomeFolder.Text += " The folder could not be created. " + error.Message;
        }
        BuildPalette();
        FillEditor();
        Show("click");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_editor.HasUnsavedChanges && !_discardConfirmed)
        {
            e.Cancel = true;
            _pending = PendingDiscard.Close;
            ShowDiscard("This macro has unsaved changes. Close anyway?");
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _tryout.Dispose();
        base.OnClosed(e);
    }

    private void ClickPage_Click(object sender, RoutedEventArgs e) => Show("click");

    private void Macros_Click(object sender, RoutedEventArgs e) => Show("library");

    private void OpenRecord_Click(object sender, RoutedEventArgs e) => Show("record");

    private void Editor_Click(object sender, RoutedEventArgs e) => Show("editor");

    private void Library_Click(object sender, RoutedEventArgs e) => Show("library");

    private void Run_Click(object sender, RoutedEventArgs e) => Show("run");

    private void Plugins_Click(object sender, RoutedEventArgs e) => Show("plugins");

    private void Settings_Click(object sender, RoutedEventArgs e) => Show("settings");

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object sender, EventArgs e)
    {
        MaxIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaxButton.SetValue(AutomationProperties.NameProperty, WindowState == WindowState.Maximized ? "Restore" : "Maximize");
        RootEdge.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int corner = 2;
        _ = DwmSetWindowAttribute(handle, 33, ref corner, sizeof(int));
    }

    private void Appearance_Checked(object sender, RoutedEventArgs e)
    {
        if (_filling || Application.Current is not App app)
        {
            return;
        }

        AppAppearance appearance = DarkBox.IsChecked == true
            ? AppAppearance.Dark
            : LightBox.IsChecked == true ? AppAppearance.Light : AppAppearance.System;
        if (appearance == app.Settings.Appearance)
        {
            return;
        }

        try
        {
            app.SaveSettings(app.Settings.WithAppearance(appearance));
            Show(_page);
            RebuildSteps();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Note("Settings could not be saved. " + error.Message);
        }
    }

    private void Trust_Changed(object sender, RoutedEventArgs e)
    {
        if (_filling || sender is not CheckBox box || Application.Current is not App app)
        {
            return;
        }

        bool accepted = box.IsChecked == true;
        if (accepted == app.Settings.ThirdPartyPluginsAcknowledged)
        {
            return;
        }

        try
        {
            app.SaveSettings(accepted ? app.Settings.Acknowledge() : app.Settings.RevokeTrust());
            _filling = true;
            TrustBox.IsChecked = accepted;
            PluginTrustBox.IsChecked = accepted;
            _filling = false;
            if (_page == "plugins")
            {
                FillPlugins();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Note("Settings could not be saved. " + error.Message);
        }
    }

    private void Show(string page)
    {
        _page = page;
        bool macros = page is "library" or "editor" or "run";
        ClickPage.Visibility = page == "click" ? Visibility.Visible : Visibility.Collapsed;
        RecordPage.Visibility = page == "record" ? Visibility.Visible : Visibility.Collapsed;
        EditorPage.Visibility = page == "editor" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = page == "library" ? Visibility.Visible : Visibility.Collapsed;
        RunPage.Visibility = page == "run" ? Visibility.Visible : Visibility.Collapsed;
        PluginsPage.Visibility = page == "plugins" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        MacroNav.Visibility = macros ? Visibility.Visible : Visibility.Collapsed;
        Mark(ClickButton, page == "click");
        Mark(MacrosButton, macros);
        Mark(RecordButton, page == "record");
        Mark(PluginsButton, page == "plugins");
        Mark(SettingsButton, page == "settings");
        Mark(EditorButton, page == "editor");
        Mark(LibraryButton, page == "library");
        Mark(RunButton, page == "run");
        if (page == "library")
        {
            FillRecent();
            FillLibrary();
        }

        if (page == "run")
        {
            ShowRun();
        }

        if (page == "plugins")
        {
            FillPlugins();
        }
    }

    private static void Mark(Button button, bool selected)
    {
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Regular;
        button.Background = selected && Application.Current?.TryFindResource("CardBrush") is Brush card
            ? card
            : Brushes.Transparent;
        button.Foreground = selected && Application.Current?.TryFindResource("ThreadBrush") is Brush thread
            ? thread
            : Application.Current?.TryFindResource("InkBrush") as Brush ?? Brushes.Black;
    }

    private void FillSettings(PluginSettings settings)
    {
        _filling = true;
        MatchWindowsBox.IsChecked = settings.Appearance == AppAppearance.System;
        LightBox.IsChecked = settings.Appearance == AppAppearance.Light;
        DarkBox.IsChecked = settings.Appearance == AppAppearance.Dark;
        TrustBox.IsChecked = settings.ThirdPartyPluginsAcknowledged;
        PluginTrustBox.IsChecked = settings.ThirdPartyPluginsAcknowledged;
        MacroFolder.Text = "Saved in " + MacroStore.DefaultDirectory() + ".";
        _filling = false;
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (TryPoint(out int x, out int y))
        {
            _tryout.MoveTo(x, y);
        }
    }

    private void Click_Click(object sender, RoutedEventArgs e)
    {
        if (TryPoint(out int x, out int y) && TryCount(out int count))
        {
            _tryout.ClickAt(x, y, count);
        }
    }

    private void Hold_Click(object sender, RoutedEventArgs e)
    {
        string key = KeyBox.Text.Trim();
        if (!MacroKeys.IsKnown(key))
        {
            Note($"Key {key} is not one of the format-1 names. F13 is a safe default.");
            return;
        }

        if (!TryRange(HoldBox, "Hold", 0, 10_000, out int hold))
        {
            return;
        }

        _tryout.HoldKey(key, hold);
    }

    private void Repeat_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPoint(out int x, out int y) || !TryCount(out int count))
        {
            return;
        }

        if (!TryRange(RepeatBox, "Times", 1, 20, out int repeat) || !TryRange(GapBox, "Gap", 0, 10_000, out int gap))
        {
            return;
        }

        _tryout.RepeatClick(x, y, count, repeat, gap);
    }

    private void Record_Click(object sender, RoutedEventArgs e) => _tryout.StartRecording(MovementBox.IsChecked == true);

    private void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        WindowPixels(out int left, out int top, out int width, out int height);
        _tryout.StopRecording(left, top, width, height);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => _tryout.PlayRecording();

    private void Pause_Click(object sender, RoutedEventArgs e) => _tryout.Pause();

    private void Resume_Click(object sender, RoutedEventArgs e) => _tryout.Resume();

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_tryout.IsRecording)
        {
            WindowPixels(out int left, out int top, out int width, out int height);
            _tryout.StopRecording(left, top, width, height);
        }

        _tryout.Stop();
    }

    private void WindowPixels(out int left, out int top, out int width, out int height)
    {
        left = 0;
        top = 0;
        width = 0;
        height = 0;
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out WindowRect rect))
        {
            return;
        }

        left = rect.Left;
        top = rect.Top;
        width = Math.Max(0, rect.Right - rect.Left);
        height = Math.Max(0, rect.Bottom - rect.Top);
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out WindowRect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    private void Refresh()
    {
        string more;
        try
        {
            more = _tryout.TakeLog();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (more.Length > 0)
        {
            LogBox.AppendText(more);
            RunLog.AppendText(more);
            LogBox.ScrollToEnd();
            RunLog.ScrollToEnd();
        }

        StatusText.Text = _tryout.State;
        ShowRun();
    }

    private bool TryPoint(out int x, out int y)
    {
        y = 0;
        return TryRange(XBox, "X", -32768, 32767, out x) && TryRange(YBox, "Y", -32768, 32767, out y);
    }

    private bool TryCount(out int count) => TryRange(CountBox, "Clicks", 1, 3, out count);

    private bool TryRange(TextBox box, string label, int minimum, int maximum, out int value)
    {
        if (int.TryParse(box.Text.Trim(), out value) && value >= minimum && value <= maximum)
        {
            return true;
        }

        Note($"{label} must be a whole number from {minimum} through {maximum}.");
        value = 0;
        return false;
    }

    private void ReleaseAfterPluginFailure()
    {
        try
        {
            _tryout.ReleaseHeldInputs();
        }
        catch (Exception error)
        {
            Note("Owned inputs could not be released. " + error.Message);
        }
    }

    private void NotePluginFaults(IReadOnlyList<ClickStitch.Plugins.PluginFault> faults)
    {
        int noted = 0;
        foreach (ClickStitch.Plugins.PluginFault fault in faults)
        {
            if (noted == 20)
            {
                Note("More plugin faults were recorded.");
                return;
            }

            Note(fault.Source + ": " + fault.Message);
            HomeFaults.Text += fault.Source + ": " + fault.Message + Environment.NewLine;
            noted++;
        }
    }

    private void Note(string message)
    {
        LogBox.AppendText(message + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

}
