using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickStitch.Core;
using Microsoft.Win32;

namespace ClickStitch;

public partial class MainWindow
{
    private void FillRecent()
    {
        HomeRecent.Children.Clear();
        IReadOnlyList<MacroFile> files = ReadLibrary();
        int shown = 0;
        foreach (MacroFile file in MacroLibrary.Order(files, MacroSort.Newest))
        {
            if (file.Problem.Length > 0)
            {
                continue;
            }

            if (shown == 5)
            {
                break;
            }

            string path = file.Path;
            var button = new Button
            {
                Content = file.Name,
                HorizontalAlignment = HorizontalAlignment.Left,
                MinHeight = 36,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 8),
            };
            button.Click += (_, _) => OpenMacro(path);
            HomeRecent.Children.Add(button);
            shown++;
        }

        if (shown == 0)
        {
            HomeRecent.Children.Add(new TextBlock
            {
                Text = "No macros yet.",
                Foreground = Brush("MutedBrush"),
            });
        }
    }

    private void LibrarySearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        FillLibrary();
    }

    private void LibrarySort_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        FillLibrary();
    }

    private void FillLibrary()
    {
        LibraryFolder.Text = "Macros are in " + MacroStore.DefaultDirectory() + ".";
        LibraryRows.Children.Clear();
        string query = LibrarySearch.Text;
        MacroSort sort = SortNewest.IsChecked == true ? MacroSort.Newest : MacroSort.Name;
        bool any = false;
        foreach (MacroFile file in MacroLibrary.Order(ReadLibrary(), sort))
        {
            if (!MacroLibrary.Matches(file, query))
            {
                continue;
            }

            any = true;
            string path = file.Path;
            string label = file.Problem.Length > 0
                ? file.Name + " — " + file.Problem
                : file.Name + " · " + file.ActionCount + (file.ActionCount == 1 ? " action" : " actions");
            var button = new Button
            {
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = 36,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 8),
                Tag = path,
            };
            if (path == _libraryPath)
            {
                button.FontWeight = FontWeights.SemiBold;
                button.Background = Brush("CardBrush");
            }

            button.Click += (_, _) => SelectLibrary(path, file.Name);
            LibraryRows.Children.Add(button);
        }

        if (!any && (LibraryStatus.Text.Length == 0 || LibraryStatus.Text == "No macros match."))
        {
            LibraryStatus.Text = "No macros match.";
        }
        else if (any && LibraryStatus.Text == "No macros match.")
        {
            LibraryStatus.Text = "";
        }
    }

    private void SelectLibrary(string path, string name)
    {
        _libraryPath = path;
        LibraryName.Text = name;
        LibraryStatus.Text = "";
        HideLibraryConfirm();
        FillLibrary();
    }

    private void LibraryOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out string path))
        {
            return;
        }

        OpenMacro(path);
    }

    private void LibraryRename_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out string path))
        {
            return;
        }

        string directory = MacroStore.DefaultDirectory();
        if (!MacroLibrary.TryRename(path, directory, LibraryName.Text, out string saved, out string error))
        {
            LibraryStatus.Text = error;
            return;
        }

        bool reopen = _savedPath == path && !_editor.HasUnsavedChanges;
        _libraryPath = saved;
        if (reopen)
        {
            LoadMacro(saved);
            return;
        }

        LibraryStatus.Text = "Renamed.";
        FillLibrary();
    }

    private void LibraryDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out string path))
        {
            return;
        }

        if (!MacroLibrary.TryDuplicate(path, MacroStore.DefaultDirectory(), out string saved, out string error))
        {
            LibraryStatus.Text = error;
            return;
        }

        _libraryPath = saved;
        LibraryStatus.Text = "A copy was saved.";
        FillLibrary();
    }

    private void LibraryAskDelete_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out _))
        {
            return;
        }

        LibraryConfirmText.Text = "Delete this macro? The file is removed from Documents.";
        LibraryConfirmText.Visibility = Visibility.Visible;
        LibraryConfirm.Visibility = Visibility.Visible;
    }

    private void LibraryKeep_Click(object sender, RoutedEventArgs e) => HideLibraryConfirm();

    private void LibraryDelete_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out string path))
        {
            return;
        }

        if (!MacroLibrary.TryDelete(path, MacroStore.DefaultDirectory(), out string error))
        {
            LibraryStatus.Text = error;
            return;
        }

        if (_savedPath == path)
        {
            _savedPath = null;
            RefreshEditorText();
        }

        _libraryPath = null;
        LibraryName.Text = "";
        HideLibraryConfirm();
        LibraryStatus.Text = "Deleted. Saving the open macro writes it again.";
        FillLibrary();
    }

    private void LibraryImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import a macro",
            Filter = "ClickStitch macro (*.clickstitch.json)|*.clickstitch.json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!MacroLibrary.TryImport(dialog.FileName, MacroStore.DefaultDirectory(), out string saved, out string error))
        {
            LibraryStatus.Text = error;
            return;
        }

        _libraryPath = saved;
        LibraryStatus.Text = "Imported " + Path.GetFileName(saved) + ".";
        FillLibrary();
    }

    private void LibraryExport_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelected(out string path))
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export a macro",
            Filter = "ClickStitch macro (*.clickstitch.json)|*.clickstitch.json",
            FileName = Path.GetFileName(path),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!MacroLibrary.TryExport(path, MacroStore.DefaultDirectory(), dialog.FileName, out string error))
        {
            LibraryStatus.Text = error;
            return;
        }

        LibraryStatus.Text = "Exported to " + dialog.FileName + ".";
    }

    private void OpenMacro(string path)
    {
        if (_editor.HasUnsavedChanges)
        {
            _pendingPath = path;
            _pending = PendingDiscard.Open;
            ShowDiscard("This macro has unsaved changes. Open the other one anyway?");
            return;
        }

        LoadMacro(path);
    }

    private void LoadMacro(string path)
    {
        MacroReadResult result = MacroStore.Load(path);
        if (result.Document is not MacroDocument document)
        {
            LibraryStatus.Text = result.Issues.Count == 0 ? "This file could not be read." : result.Issues[0].Message;
            Show("library");
            return;
        }

        _editor = MacroEditor.Open(document);
        _savedPath = path;
        _libraryPath = path;
        LibraryName.Text = document.Name;
        FillEditor();
        Show("editor");
    }

    private void RunMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Document is not MacroDocument document)
        {
            foreach (string problem in _editor.DocumentProblems)
            {
                RunLog.AppendText(problem + Environment.NewLine);
            }

            foreach (EditorStep step in _editor.Steps)
            {
                foreach (string problem in step.Problems)
                {
                    RunLog.AppendText(problem + Environment.NewLine);
                }
            }

            RunLog.AppendText("Fix the macro before running it." + Environment.NewLine);
            RunLog.ScrollToEnd();
            return;
        }

        _tryout.PlayMacro(document.Name, MacroJson.Write(document));
    }

    private void ShowRun()
    {
        RunMacro.Text = _editor.Name;
        RunState.Text = "State: " + _tryout.State;
        RunElapsed.Text = "Elapsed: " + _tryout.ElapsedText;
        string step = _tryout.CurrentStep;
        string line = step;
        foreach (EditorStep item in _editor.Steps)
        {
            if (item.Id == step)
            {
                line = item.Line;
                break;
            }
        }

        RunAction.Text = line.Length == 0 ? "" : "Now: " + line;
    }

    private bool TrySelected(out string path)
    {
        path = _libraryPath ?? "";
        if (path.Length > 0)
        {
            return true;
        }

        LibraryStatus.Text = "Choose a macro first.";
        return false;
    }

    private void HideLibraryConfirm()
    {
        LibraryConfirm.Visibility = Visibility.Collapsed;
        LibraryConfirmText.Visibility = Visibility.Collapsed;
    }

    private static IReadOnlyList<MacroFile> ReadLibrary()
    {
        try
        {
            return MacroLibrary.Read(MacroStore.DefaultDirectory());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
