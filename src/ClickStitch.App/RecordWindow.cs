using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ClickStitch.Core;

namespace ClickStitch;

public partial class MainWindow
{
    private string? _recordingDeletePath;

    private void FinishRecording()
    {
        WindowPixels(out int left, out int top, out int width, out int height);
        _tryout.StopRecording(left, top, width, height);
        SaveLatestRecording();
        FillRecordings();
    }

    private void SaveLatestRecording()
    {
        if (_tryout.RecordingJson is not string json)
        {
            return;
        }

        MacroReadResult read = MacroJson.Read(json);
        if (read.Document is not MacroDocument document)
        {
            Note(read.Issues.Count == 0 ? "The recording could not be saved." : read.Issues[0].Message);
            return;
        }

        try
        {
            MacroStore.Save(document, MacroStore.RecordingsDirectory());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Note("The recording could not be saved. " + error.Message);
        }
    }

    private void FillRecordings()
    {
        RecordingList.Children.Clear();
        List<MacroFile> files = MacroLibrary.Read(MacroStore.RecordingsDirectory())
            .OrderByDescending(file => file.ModifiedUtc)
            .ToList();
        RecordingEmpty.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (MacroFile file in files)
        {
            RecordingList.Children.Add(RecordingRow(file));
        }
    }

    private MacroFile? NewestRecording() =>
        MacroLibrary.Read(MacroStore.RecordingsDirectory())
            .Where(file => file.Problem.Length == 0)
            .OrderByDescending(file => file.ModifiedUtc)
            .FirstOrDefault();

    private UIElement RecordingRow(MacroFile file)
    {
        var play = new Button
        {
            Width = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            Tag = file.Path,
            ToolTip = "Play this recording",
            IsEnabled = file.Problem.Length == 0,
            Content = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Text = "\uE768",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(play, "Play " + file.Name);
        play.Click += PlaySavedRecording_Click;

        var title = new TextBlock
        {
            Text = file.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var detail = new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 0),
            FontSize = 12,
            Text = file.Problem.Length == 0
                ? file.ModifiedUtc.ToLocalTime().ToString("g") + " · " + file.ActionCount + " actions"
                : file.Problem,
            TextWrapping = TextWrapping.Wrap,
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

        var delete = new Button
        {
            Content = "Delete",
            MinWidth = 64,
            Margin = new Thickness(8, 0, 0, 0),
            Tag = file,
            ToolTip = "Delete this recording",
        };
        AutomationProperties.SetName(delete, "Delete " + file.Name);
        delete.Click += AskDeleteRecording_Click;

        var text = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        text.Children.Add(title);
        text.Children.Add(detail);

        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(delete, 2);
        row.Children.Add(play);
        row.Children.Add(text);
        row.Children.Add(delete);
        return row;
    }

    private void PlaySavedRecording_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }

        PlayPath(path);
    }

    private void PlaySaved(MacroFile file) => PlayPath(file.Path);

    private void PlayPath(string path)
    {
        MacroReadResult loaded = MacroStore.Load(path);
        if (loaded.Document is not MacroDocument document)
        {
            Note(loaded.Issues.Count == 0 ? "This recording could not be read." : loaded.Issues[0].Message);
            return;
        }

        _tryout.PlayMacro(document.Name, MacroJson.Write(document));
    }

    private void AskDeleteRecording_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MacroFile file })
        {
            return;
        }

        _recordingDeletePath = file.Path;
        RecordingConfirmText.Text = "Delete " + file.Name + "? The file is removed from this PC.";
        RecordingConfirm.Visibility = Visibility.Visible;
    }

    private void KeepRecording_Click(object sender, RoutedEventArgs e)
    {
        _recordingDeletePath = null;
        RecordingConfirm.Visibility = Visibility.Collapsed;
    }

    private void DeleteRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingDeletePath is not string path)
        {
            return;
        }

        if (!MacroLibrary.TryDelete(path, MacroStore.RecordingsDirectory(), out string error))
        {
            Note(error);
            return;
        }

        _recordingDeletePath = null;
        RecordingConfirm.Visibility = Visibility.Collapsed;
        Note("Recording deleted.");
        FillRecordings();
    }
}
