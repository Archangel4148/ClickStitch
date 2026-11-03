using ClickStitch.Plugins;

namespace ClickStitch.SamplePlugin;

/// <summary>
/// Registers one action. It does not send input. Build this project against ClickStitch.Plugins.Sdk only.
/// </summary>
public sealed class Plugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new(
        "com.clickstitch.sample",
        "Sample",
        new PluginVersion(1, 0, 0),
        PluginApi.Current);

    public void Initialize(ICoreHost host)
    {
        host.Log("Sample plugin ready.");
        host.RegisterAction(
            new PluginActionDescriptor("sample.ping", "Ping", "Reports that the sample plugin ran.", "{}"),
            static context =>
            {
                context.Cancellation.ThrowIfCancellationRequested();
                context.Report("Sample action ran.");
            });
    }

    public void Shutdown()
    {
    }
}
