using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickStitch.Core;
using ClickStitch.Input;
using ClickStitch.Plugins;

namespace ClickStitch;

public partial class MainWindow
{
    private enum Arm
    {
        None,
        Target,
        Hotkey,
    }

    private Arm _arm;

    private void FillClickForm()
    {
        _filling = true;
        TargetBox.Items.Clear();
        TargetBox.Items.Add("Left button");
        TargetBox.Items.Add("Right button");
        TargetBox.Items.Add("Middle button");
        foreach (string key in MacroKeys.All)
        {
            TargetBox.Items.Add(key);
        }

        TargetBox.SelectedIndex = 0;
        TargetLabel.Text = "Left button";
        _filling = false;
        RefreshClickOptions();
    }

    private int _listClosedAt;

    private void TargetFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (Environment.TickCount - _listClosedAt < 250)
        {
            TargetPopup.IsOpen = false;
        }
        else
        {
            TargetPopup.IsOpen = !TargetPopup.IsOpen;
        }

        e.Handled = true;
    }

    private void TargetList_Closed(object? sender, EventArgs e) => _listClosedAt = Environment.TickCount;

    private void Target_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_filling)
        {
            return;
        }

        if (TargetBox.SelectedItem is string name)
        {
            TargetLabel.Text = name;
        }

        TargetPopup.IsOpen = false;
        RefreshClickOptions();
    }

    private void ClickForm_Changed(object sender, RoutedEventArgs e)
    {
        if (_filling || SpamOptions is null)
        {
            return;
        }

        RefreshClickOptions();
    }

    private void RefreshClickOptions()
    {
        if (TargetBox is null || SpamOptions is null || ClickTypeRow is null || PlaceRow is null || PointFields is null || RepeatBox is null || SpamMode is null || CountModeBox is null || PointBox is null)
        {
            return;
        }

        bool mouse = TargetIsMouse();
        bool spam = SpamMode.IsChecked == true;
        if (TargetGlyph is not null)
        {
            TargetGlyph.Text = mouse ? "\uE962" : "\uE765";
        }
        SpamOptions.Visibility = spam ? Visibility.Visible : Visibility.Collapsed;
        ClickTypeRow.Visibility = spam && mouse ? Visibility.Visible : Visibility.Collapsed;
        PlaceRow.Visibility = mouse ? Visibility.Visible : Visibility.Collapsed;
        PointFields.Visibility = mouse && PointBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RepeatBox.IsEnabled = CountModeBox.IsChecked == true;
    }

    private void ArmTarget_Click(object sender, RoutedEventArgs e)
    {
        _arm = Arm.Target;
        ArmTargetButton.Content = "Listening";
        ArmHotkeyButton.Content = "Set";
        ArmHint.Visibility = Visibility.Visible;
        ArmHint.Text = "Press a mouse button or a key. Escape cancels.";
    }

    private void ArmHotkey_Click(object sender, RoutedEventArgs e)
    {
        _arm = Arm.Hotkey;
        ArmHotkeyButton.Content = "Listening";
        ArmTargetButton.Content = "Press";
        ArmHint.Visibility = Visibility.Visible;
        ArmHint.Text = "Press the shortcut. Escape cancels.";
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        Disarm();
        _hotkey?.Clear();
        HotkeyLabel.Text = "None";
        SaveHotkey(null);
        ClickStatus.Text = "Shortcut cleared.";
    }

    private void CaptureMouse(object sender, MouseButtonEventArgs e)
    {
        if (_arm != Arm.Target)
        {
            return;
        }

        string? name = e.ChangedButton switch
        {
            MouseButton.Left => "Left button",
            MouseButton.Right => "Right button",
            MouseButton.Middle => "Middle button",
            _ => null,
        };
        if (name is null)
        {
            ArmHint.Text = "Use the left, right, or middle button.";
            e.Handled = true;
            return;
        }

        SelectTarget(name);
        e.Handled = true;
    }

    private void CaptureKey(object sender, KeyEventArgs e)
    {
        if (_arm == Arm.None)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            Disarm();
            e.Handled = true;
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        string? name = SchemaKey(key);
        if (_arm == Arm.Target)
        {
            if (name is null)
            {
                ArmHint.Text = "That key is not available.";
                e.Handled = true;
                return;
            }

            SelectTarget(name);
            e.Handled = true;
            return;
        }

        if (name is null || name is "Shift" or "Control" or "Alt" or "LeftWindows" or "RightWindows")
        {
            return;
        }

        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(name);
        string problem = "";
        if (_hotkey is null || !_hotkey.TryBind(string.Join('+', parts), out problem))
        {
            ClickStatus.Text = _hotkey is null ? "The shortcut is not ready yet." : problem;
            Disarm();
            e.Handled = true;
            return;
        }

        HotkeyLabel.Text = _hotkey.Bound ?? name;
        SaveHotkey(_hotkey.Bound);
        ClickStatus.Text = "Shortcut saved.";
        Disarm();
        e.Handled = true;
    }

    private void PickPoint_Click(object sender, RoutedEventArgs e)
    {
        Disarm();
        if (!PointPick.TryPick(this, out int x, out int y))
        {
            ClickStatus.Text = "Point pick canceled.";
            return;
        }

        XBox.Text = x.ToString(CultureInfo.InvariantCulture);
        YBox.Text = y.ToString(CultureInfo.InvariantCulture);
        ClickStatus.Text = "Point set. Start clicks there.";
    }

    private void StartClick_Click(object sender, RoutedEventArgs e)
    {
        if (PointPick.IsOpen)
        {
            return;
        }

        Disarm();
        string target = TargetToken();
        bool atCursor = !TargetIsMouse() || HereBox.IsChecked == true;
        int x = 0;
        int y = 0;
        if (!atCursor && (!int.TryParse(XBox.Text.Trim(), out x) || !int.TryParse(YBox.Text.Trim(), out y)))
        {
            ClickStatus.Text = "X and Y must be whole numbers.";
            return;
        }

        int times = ClickRecipe.UntilStopped;
        if (CountModeBox.IsChecked == true && !int.TryParse(RepeatBox.Text.Trim(), out times))
        {
            ClickStatus.Text = "Count must be a whole number.";
            return;
        }

        if (!int.TryParse(GapBox.Text.Trim(), out int gap))
        {
            ClickStatus.Text = "The gap must be a whole number of milliseconds.";
            return;
        }

        int clickCount = TripleBox.IsChecked == true ? 3 : DoubleBox.IsChecked == true ? 2 : 1;
        if (!ClickRecipe.TryBuild(target, HoldMode.IsChecked == true, atCursor, x, y, clickCount, times, gap, out string json, out string problem))
        {
            ClickStatus.Text = problem;
            return;
        }

        _tryout.PlayMacro("Click", json);
        ClickStatus.Text = HoldMode.IsChecked == true
            ? "Holding. Stop lets go. It also lets go after an hour."
            : "Started. Stop ends it.";
    }

    private void ToggleFromHotkey()
    {
        if (_arm != Arm.None || PointPick.IsOpen)
        {
            return;
        }

        string state = _tryout.State;
        if (state is "Running" or "Paused" or "Stopping" or "Recording")
        {
            Stop_Click(this, new RoutedEventArgs());
            return;
        }

        StartClick_Click(this, new RoutedEventArgs());
    }

    private void SelectTarget(string name)
    {
        _filling = true;
        TargetBox.SelectedItem = name;
        TargetLabel.Text = name;
        _filling = false;
        Disarm();
        RefreshClickOptions();
    }

    private void Disarm()
    {
        _arm = Arm.None;
        ArmTargetButton.Content = "Press";
        ArmHotkeyButton.Content = "Set";
        ArmHint.Visibility = Visibility.Collapsed;
    }

    private bool TargetIsMouse() => TargetToken() is "left" or "right" or "middle";

    private string TargetToken()
    {
        string text = TargetBox.SelectedItem as string ?? "Left button";
        return text switch
        {
            "Left button" => "left",
            "Right button" => "right",
            "Middle button" => "middle",
            _ => text,
        };
    }

    private static string? SchemaKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return "Digit" + ((int)key - (int)Key.D0);
        }

        return key switch
        {
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftCtrl or Key.RightCtrl => "Control",
            Key.LeftAlt or Key.RightAlt or Key.System => "Alt",
            Key.LWin => "LeftWindows",
            Key.RWin => "RightWindows",
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Tab => "Tab",
            Key.Escape => "Escape",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemMinus => "Minus",
            Key.OemPlus => "Equal",
            Key.Oem1 => "Semicolon",
            Key.Oem2 => "Slash",
            Key.Oem5 => "Backslash",
            Key.Oem7 => "Apostrophe",
            Key.Oem4 => "LeftBracket",
            Key.Oem6 => "RightBracket",
            _ => null,
        };
    }

    private void SaveHotkey(string? chord)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        try
        {
            app.SaveSettings(app.Settings.WithToggleHotkey(chord));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ClickStatus.Text = "The shortcut could not be saved. " + error.Message;
        }
    }
}
