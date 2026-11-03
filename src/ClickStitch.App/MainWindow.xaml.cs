using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClickStitch.Core;
using ClickStitch.Input;

namespace ClickStitch;

/// <summary>
/// Prototype tryout. The product editor is a later phase.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DesktopTryout _tryout;
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ClickStitch";
        Heading.Text = "ClickStitch";
        ApplyHighContrast();
        _tryout = DesktopTryout.Create();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _tryout.Dispose();
        base.OnClosed(e);
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

    private void Pause_Click(object sender, RoutedEventArgs e) => _tryout.Pause();

    private void Resume_Click(object sender, RoutedEventArgs e) => _tryout.Resume();

    private void Stop_Click(object sender, RoutedEventArgs e) => _tryout.Stop();

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
            LogBox.ScrollToEnd();
        }

        StatusText.Text = _tryout.State;
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

    private void Note(string message)
    {
        LogBox.AppendText(message + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void ApplyHighContrast()
    {
        if (!SystemParameters.HighContrast)
        {
            return;
        }

        Background = SystemColors.WindowBrush;
        Foreground = SystemColors.WindowTextBrush;
        AccentBar.Background = SystemColors.HighlightBrush;
        StopButton.Background = SystemColors.HighlightBrush;
        StopButton.Foreground = SystemColors.HighlightTextBrush;
    }
}
