using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClickStitch.Plugins;

namespace ClickStitch;

public partial class MainWindow
{
    private void FillPlugins()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        _filling = true;
        PluginsFolder.Text = "Installed plugins are read from " + Path.Combine(AppContext.BaseDirectory, PluginDiscovery.DirectoryName) + ".";
        PluginTrustBox.IsChecked = app.Settings.ThirdPartyPluginsAcknowledged;
        PluginRows.Children.Clear();
        IReadOnlyList<PluginListing> listings = PluginManager.List(app.Plugins, app.Settings, app.PluginRuntime.LoadedIds);
        if (listings.Count == 0)
        {
            PluginRows.Children.Add(new TextBlock
            {
                Text = "No plugins are installed.",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        foreach (PluginListing listing in listings)
        {
            PluginRows.Children.Add(PluginCard(listing, app.Settings.ThirdPartyPluginsAcknowledged));
        }

        IReadOnlyList<PluginFault> errors = PluginManager.Errors(app.Plugins, app.PluginRuntime.Faults);
        PluginProblems.Text = errors.Count == 0
            ? "None."
            : string.Join(Environment.NewLine, errors.Select(fault => fault.Source + ": " + fault.Message));
        _filling = false;
    }

    private Border PluginCard(PluginListing listing, bool trusted)
    {
        var toggle = new CheckBox
        {
            Content = "On",
            IsChecked = listing.Enabled && trusted,
            IsEnabled = trusted && listing.Compatible,
            Tag = listing.Id,
            Margin = new Thickness(0, 8, 0, 0),
        };
        toggle.Checked += PluginToggle;
        toggle.Unchecked += PluginToggle;
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = listing.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text = listing.Id,
            FontSize = 13,
            Foreground = Brush("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "Version " + listing.Version + " · API " + listing.ApiVersion + (listing.Compatible ? " · Compatible" : " · Not compatible"),
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock { Text = listing.State, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(toggle);
        return new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            Background = Brush("CardBrush"),
            Child = body,
        };
    }

    private void PluginToggle(object sender, RoutedEventArgs e)
    {
        if (_filling || sender is not CheckBox box || box.Tag is not string id || Application.Current is not App app)
        {
            return;
        }

        try
        {
            app.SaveSettings(box.IsChecked == true ? app.Settings.Enable(id) : app.Settings.Disable(id));
            PluginStatus.Text = "Saved. This takes effect the next time ClickStitch starts.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            PluginStatus.Text = error.Message;
        }

        FillPlugins();
    }
}
