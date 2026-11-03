namespace ClickStitch.Plugins.Probe;

public static class ProbeLog
{
    public static List<string> Stops { get; } = [];

    public static List<string> Events { get; } = [];

    public static ICoreHost? LateHost { get; set; }

    public static int Started { get; set; }

    public static PluginEventHandler? StartedHandler { get; set; }
}

public sealed class AlphaPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.alpha", "Alpha", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host)
    {
        host.Log("ready");
        host.RegisterAction(new PluginActionDescriptor("alpha.ping", "Ping", "Says hello.", "{}"), context => context.Report("pong"));
    }

    public void Shutdown() => ProbeLog.Stops.Add(Metadata.Id);
}

public sealed class BetaPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.beta", "Beta", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) =>
        host.RegisterAction(new PluginActionDescriptor("beta.ping", "Ping", "Says hello.", "{}"), _ => { });

    public void Shutdown() => ProbeLog.Stops.Add(Metadata.Id);
}

public sealed class DuplicateActionPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.duplicate", "Duplicate", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) =>
        host.RegisterAction(new PluginActionDescriptor("alpha.ping", "Ping", "Says hello.", "{}"), _ => { });

    public void Shutdown() => ProbeLog.Stops.Add(Metadata.Id);
}

public sealed class GammaPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.gamma", "Gamma", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host)
    {
        host.Subscribe(PluginEventKind.MacroStarted, _ => ProbeLog.Events.Add("gamma"));
        throw new InvalidOperationException("Gamma failed.");
    }

    public void Shutdown() => ProbeLog.Stops.Add(Metadata.Id);
}

public sealed class MismatchPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.mismatch", "Mismatch", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) => host.Log("should not run");

    public void Shutdown() => ProbeLog.Stops.Add(Metadata.Id);
}

public sealed class EpsilonPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.epsilon", "Epsilon", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host)
    {
        ProbeLog.LateHost = host;
        ProbeLog.StartedHandler = _ => ProbeLog.Started++;
        host.Subscribe(PluginEventKind.MacroStarted, ProbeLog.StartedHandler);
    }

    public void Shutdown()
    {
    }
}

public sealed class BoomPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.boom", "Boom", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) =>
        host.RegisterAction(new PluginActionDescriptor("alpha.boom", "Boom", "Fails on purpose.", "{}"), _ => throw new InvalidOperationException("Boom."));

    public void Shutdown()
    {
    }
}

public sealed class FragilePlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.fragile", "Fragile", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host)
    {
    }

    public void Shutdown() => throw new InvalidOperationException("Fragile shutdown.");
}

public sealed class EventPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.events", "Events", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) =>
        host.Subscribe(PluginEventKind.MacroStarted, kind => ProbeLog.Events.Add(kind.ToString()));

    public void Shutdown()
    {
    }
}

public sealed class BadEventPlugin : IMacroPlugin
{
    public PluginMetadata Metadata { get; } = new("com.clickstitch.badevent", "Bad event", new PluginVersion(1, 0, 0), PluginApi.Current);

    public void Initialize(ICoreHost host) =>
        host.Subscribe(PluginEventKind.MacroStarted, _ => throw new InvalidOperationException("Event failed."));

    public void Shutdown()
    {
    }
}

public sealed class NotAPlugin
{
}
