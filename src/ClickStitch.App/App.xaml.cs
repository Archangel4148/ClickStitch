using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClickStitch.Plugins;

namespace ClickStitch;

/// <summary>
/// Foundation shell. Startup reads a Plugins folder and loads only plugins that were enabled after the trust warning.
/// A missing folder, a bad settings file, or a plugin that fails to start still opens the window.
/// No input hook is installed here.
/// </summary>
public partial class App : Application
{
    public PluginCatalog Plugins { get; private set; } = PluginCatalog.Empty;

    public PluginHost PluginRuntime { get; private set; } = PluginHost.Load(PluginCatalog.Empty);

    public PluginSettings Settings { get; private set; } = PluginSettings.Off;

    public void SaveSettings(PluginSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        PluginSettingsStore.Write(PluginSettingsStore.DefaultPath, settings);
        ShellTheme.Apply(this, settings.Appearance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OpenComboFromItsBody));
        try
        {
            Plugins = PluginDiscovery.Discover(Path.Combine(AppContext.BaseDirectory, PluginDiscovery.DirectoryName));
            PluginSettingsLoad loaded = PluginSettingsStore.Read(PluginSettingsStore.DefaultPath);
            Settings = loaded.Settings;
            PluginRuntime = PluginHost.LoadEnabled(Plugins, loaded);
        }
        catch (Exception)
        {
            Plugins = PluginCatalog.Empty;
            Settings = PluginSettings.Off;
            PluginRuntime = PluginHost.Load(PluginCatalog.Empty);
        }

        ShellTheme.Apply(this, Settings.Appearance);
        base.OnStartup(e);
    }

    private static void OpenComboFromItsBody(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox box || box.IsEditable || e.OriginalSource is not DependencyObject source || IsComboToggle(source))
        {
            return;
        }

        box.IsDropDownOpen = !box.IsDropDownOpen;
        e.Handled = true;
    }

    private static bool IsComboToggle(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            PluginRuntime.Shutdown();
        }
        catch (Exception)
        {
            // Shutdown already records plugin failures. A failure here must not block exit.
        }

        base.OnExit(e);
    }
}
