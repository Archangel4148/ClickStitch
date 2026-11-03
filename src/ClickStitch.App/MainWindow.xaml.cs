using System.Windows;
using ClickStitch.Core;

namespace ClickStitch;

/// <summary>
/// Opens the foundation window. It does not start a macro.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "ClickStitch";
        Heading.Text = "ClickStitch";
        FormatText.Text = $"Macro documents use format version {MacroFormat.CurrentVersion}.";
        ApplyHighContrast();
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
    }
}
