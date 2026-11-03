using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ClickStitch.Core;

namespace ClickStitch;

public partial class MainWindow
{
    private sealed class StepRow
    {
        public required string Path { get; init; }

        public required TextBlock Line { get; init; }

        public required TextBlock Problems { get; init; }
    }

    private void NewMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.HasUnsavedChanges)
        {
            _pending = PendingDiscard.Replace;
            ShowDiscard("This macro has unsaved changes. Replace it with a new one?");
            return;
        }

        ReplaceEditor();
        Show("editor");
    }

    private void EditorDiscard_Click(object sender, RoutedEventArgs e)
    {
        PendingDiscard pending = _pending;
        string? path = _pendingPath;
        _pending = PendingDiscard.None;
        HideDiscard();
        if (pending == PendingDiscard.Close)
        {
            _discardConfirmed = true;
            Close();
            return;
        }

        if (pending == PendingDiscard.Open && path is not null)
        {
            LoadMacro(path);
            return;
        }

        ReplaceEditor();
    }

    private void EditorKeep_Click(object sender, RoutedEventArgs e)
    {
        _pending = PendingDiscard.None;
        HideDiscard();
    }

    private void EditorName_Changed(object sender, TextChangedEventArgs e)
    {
        if (_filling)
        {
            return;
        }

        _editor.SetName(EditorName.Text);
        RefreshEditorText();
    }

    private void EditorDescription_Changed(object sender, TextChangedEventArgs e)
    {
        if (_filling)
        {
            return;
        }

        _editor.SetDescription(EditorDescription.Text);
        RefreshEditorText();
    }

    private void EditorUndo_Click(object sender, RoutedEventArgs e)
    {
        if (!_editor.CanUndo)
        {
            return;
        }

        _editor.Undo();
        FillEditor();
    }

    private void EditorRedo_Click(object sender, RoutedEventArgs e)
    {
        if (!_editor.CanRedo)
        {
            return;
        }

        _editor.Redo();
        FillEditor();
    }

    private void EditorSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_editor.TrySave(MacroStore.DefaultDirectory(), out string path))
            {
                RefreshEditorText();
                return;
            }

            _savedPath = path;
            RefreshEditorText();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            EditorStatus.Text = "The macro could not be saved. " + error.Message;
        }
    }

    private void ShowDiscard(string message)
    {
        EditorBanner.Text = message;
        EditorBanner.Visibility = Visibility.Visible;
        EditorChoiceRow.Visibility = Visibility.Visible;
        Show("editor");
    }

    private void HideDiscard()
    {
        EditorBanner.Visibility = Visibility.Collapsed;
        EditorChoiceRow.Visibility = Visibility.Collapsed;
    }

    private void ReplaceEditor()
    {
        _editor = MacroEditor.Create();
        _savedPath = null;
        HideDiscard();
        FillEditor();
    }

    private void BuildPalette()
    {
        foreach (MacroActionDescriptor descriptor in MacroActionCatalog.BuiltIn)
        {
            string typeId = descriptor.TypeId;
            var button = new Button
            {
                Content = descriptor.DisplayName,
                MinHeight = 36,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 8),
                ToolTip = descriptor.Description,
            };
            AutomationProperties.SetName(button, "Add " + descriptor.DisplayName);
            button.Click += (_, _) =>
            {
                _editor.Add(typeId, null);
                RebuildSteps();
                RefreshEditorText();
            };
            EditorPalette.Children.Add(button);
        }
    }

    private void FillEditor()
    {
        _filling = true;
        EditorName.Text = _editor.Name;
        EditorDescription.Text = _editor.Description;
        _filling = false;
        RebuildSteps();
        RefreshEditorText();
    }

    private void RebuildSteps()
    {
        _filling = true;
        EditorSteps.Children.Clear();
        _rows.Clear();
        foreach (EditorStep step in _editor.Steps)
        {
            EditorSteps.Children.Add(BuildStep(step));
        }

        _filling = false;
    }

    private void RefreshEditorText()
    {
        EditorPreview.Text = _editor.Preview;
        EditorProblems.Text = string.Join(Environment.NewLine, _editor.DocumentProblems);
        EditorUndoButton.IsEnabled = _editor.CanUndo;
        EditorRedoButton.IsEnabled = _editor.CanRedo;
        if (_editor.HasUnsavedChanges)
        {
            EditorStatus.Text = _editor.Document is null ? "Fix the problems before saving." : "Not saved yet.";
        }
        else if (_savedPath is not null)
        {
            EditorStatus.Text = "Saved to " + _savedPath + ".";
        }
        else
        {
            EditorStatus.Text = "No changes to save.";
        }

        foreach (StepRow row in _rows)
        {
            foreach (EditorStep step in _editor.Steps)
            {
                if (step.Path != row.Path)
                {
                    continue;
                }

                row.Line.Text = step.Line;
                row.Problems.Text = string.Join(Environment.NewLine, step.Problems);
                break;
            }
        }
    }

    private Border BuildStep(EditorStep step)
    {
        var line = new TextBlock
        {
            Text = step.Line,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var problems = new TextBlock
        {
            Text = string.Join(Environment.NewLine, step.Problems),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = Brush("ThreadBrush"),
        };
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = step.Category + " · " + step.Title,
            FontWeight = FontWeights.SemiBold,
        });
        body.Children.Add(line);
        foreach (EditorField field in step.Fields)
        {
            body.Children.Add(BuildField(step.Path, field));
        }

        body.Children.Add(problems);
        body.Children.Add(BuildStepButtons(step));
        _rows.Add(new StepRow { Path = step.Path, Line = line, Problems = problems });
        return new Border
        {
            Margin = new Thickness(step.Depth * 24, 0, 0, 8),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            Background = Brush("CardBrush"),
            BorderBrush = CategoryBrush(step.Category),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Child = body,
        };
    }

    private StackPanel BuildField(string path, EditorField field)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(new TextBlock
        {
            Text = field.Label,
            FontSize = 13,
            Foreground = Brush("MutedBrush"),
        });
        stack.Children.Add(ChoiceOrText(path, field));
        return stack;
    }

    private Control ChoiceOrText(string path, EditorField field)
    {
        foreach (EditorChoice choice in field.Choices)
        {
            if (choice.Value == field.Value)
            {
                var box = new ComboBox
                {
                    MinHeight = 36,
                    MinWidth = 180,
                    ItemsSource = field.Choices,
                    DisplayMemberPath = nameof(EditorChoice.Label),
                    SelectedItem = choice,
                    ToolTip = field.Help,
                };
                box.SelectionChanged += (_, _) =>
                {
                    if (_filling || box.SelectedItem is not EditorChoice selected)
                    {
                        return;
                    }

                    _editor.SetField(path, field.Name, selected.Value);
                    if (field.Name == "coordinateMode")
                    {
                        RebuildSteps();
                    }

                    RefreshEditorText();
                };
                return box;
            }
        }

        var text = new TextBox
        {
            Text = field.Value,
            MinHeight = 36,
            ToolTip = field.Help,
        };
        text.TextChanged += (_, _) =>
        {
            if (_filling)
            {
                return;
            }

            _editor.SetField(path, field.Name, text.Text);
            RefreshEditorText();
        };
        return text;
    }

    private WrapPanel BuildStepButtons(EditorStep step)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(StepButton("Up", "Move this action up", () => MoveStep(step.Path, -1)));
        row.Children.Add(StepButton("Down", "Move this action down", () => MoveStep(step.Path, 1)));
        row.Children.Add(StepButton("Duplicate", "Duplicate this action", () =>
        {
            _editor.Duplicate(step.Path);
            RebuildSteps();
            RefreshEditorText();
        }));
        row.Children.Add(StepButton("Delete", "Delete this action", () =>
        {
            _editor.Remove(step.Path);
            RebuildSteps();
            RefreshEditorText();
        }));
        if (!step.IsRepeat)
        {
            return row;
        }

        var choices = new ComboBox { MinHeight = 36, MinWidth = 140, Margin = new Thickness(0, 0, 8, 8) };
        foreach (MacroActionDescriptor descriptor in MacroActionCatalog.BuiltIn)
        {
            choices.Items.Add(new ComboBoxItem { Content = descriptor.DisplayName, Tag = descriptor.TypeId });
        }

        choices.SelectedIndex = 0;
        row.Children.Add(choices);
        row.Children.Add(StepButton("Add inside", "Add an action inside this repeat", () =>
        {
            if (choices.SelectedItem is ComboBoxItem item && item.Tag is string typeId)
            {
                _editor.Add(typeId, step.Path);
                RebuildSteps();
                RefreshEditorText();
            }
        }));
        return row;
    }

    private void MoveStep(string path, int direction)
    {
        if (!_editor.Move(path, direction))
        {
            return;
        }

        RebuildSteps();
        RefreshEditorText();
    }

    private static Button StepButton(string content, string name, Action click)
    {
        var button = new Button
        {
            Content = content,
            MinHeight = 36,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 8),
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);

    private static Brush CategoryBrush(string category)
    {
        Color color = category switch
        {
            "Keyboard" => Color.FromRgb(0x6B, 0x4C, 0x9A),
            "Wait" => Color.FromRgb(0xC4, 0x84, 0x1D),
            "Flow" => Color.FromRgb(0x2A, 0x7A, 0x6A),
            "Note" => Color.FromRgb(0x3D, 0x4A, 0x5C),
            _ => Color.FromRgb(0x2F, 0x6F, 0xED),
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
