using System.IO;
using System.Windows;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Plugins = PluginDiscovery.Discover(Path.Combine(AppContext.BaseDirectory, PluginDiscovery.DirectoryName));
            PluginRuntime = PluginHost.LoadEnabled(Plugins, PluginSettingsStore.Read(PluginSettingsStore.DefaultPath));
        }
        catch (Exception)
        {
            Plugins = PluginCatalog.Empty;
            PluginRuntime = PluginHost.Load(PluginCatalog.Empty);
        }

        base.OnStartup(e);
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
