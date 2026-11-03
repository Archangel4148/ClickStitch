using ClickStitch.Plugins.Probe;

namespace ClickStitch.Plugins.Host.Tests;

public sealed class PluginHostTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "clickstitch-plugins-" + Guid.NewGuid().ToString("N"));

    public PluginHostTests()
    {
        ProbeLog.Stops.Clear();
        ProbeLog.Events.Clear();
        ProbeLog.LateHost = null;
        ProbeLog.Started = 0;
        ProbeLog.StartedHandler = null;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void A_missing_plugins_directory_is_empty()
    {
        PluginCatalog catalog = PluginDiscovery.Discover(Path.Combine(_root, "missing"));

        Assert.Empty(catalog.Plugins);
        Assert.Empty(catalog.Faults);
    }

    [Fact]
    public void Discovery_keeps_a_compatible_plugin_and_skips_the_rest()
    {
        WritePlugin("a-ok", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-same", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("c-new-api", "com.clickstitch.newapi", "2.0", "AlphaPlugin");
        WritePlugin("d-broken", "not an id", "1.0", "AlphaPlugin");
        Directory.CreateDirectory(Path.Combine(_root, "e-empty"));
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "ignored");

        PluginCatalog catalog = PluginDiscovery.Discover(_root);

        DiscoveredPlugin found = Assert.Single(catalog.Plugins);
        Assert.Equal("com.clickstitch.alpha", found.Manifest.Metadata.Id);
        Assert.Contains(catalog.Faults, fault => fault.Source == "com.clickstitch.alpha" && fault.Message == "Plugin id com.clickstitch.alpha is already used.");
        Assert.Contains(catalog.Faults, fault => fault.Source == "c-new-api" && fault.Message.Contains("2.0", StringComparison.Ordinal));
        Assert.Contains(catalog.Faults, fault => fault.Source == "d-broken");
        Assert.Contains(catalog.Faults, fault => fault.Source == "e-empty" && fault.Message == "The plugin manifest was not found.");
    }

    [Fact]
    public void Load_registers_the_action_and_shutdown_runs_in_reverse()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-beta", "com.clickstitch.beta", "1.0", "BetaPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));

        Assert.Equal(["com.clickstitch.alpha", "com.clickstitch.beta"], host.LoadedIds);
        Assert.Equal(["alpha.ping", "beta.ping"], host.Actions.Select(action => action.Id).ToArray());
        Assert.Contains("com.clickstitch.alpha: ready", host.Log);

        host.Shutdown();
        host.Shutdown();

        Assert.Equal(["com.clickstitch.beta", "com.clickstitch.alpha"], ProbeLog.Stops);
        Assert.Empty(host.LoadedIds);
    }

    [Fact]
    public void A_failed_plugin_is_skipped_and_does_not_shut_down()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-gamma", "com.clickstitch.gamma", "1.0", "GammaPlugin");
        WritePlugin("c-missing", "com.clickstitch.missing", "1.0", "AlphaPlugin", copyAssembly: false);
        WritePlugin("d-type", "com.clickstitch.notype", "1.0", "NotAPlugin");
        WritePlugin("e-mismatch", "com.clickstitch.other", "1.0", "MismatchPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));

        Assert.Equal(["com.clickstitch.alpha"], host.LoadedIds);
        Assert.Contains(host.Faults, fault => fault.Message == "Plugin initialization failed. Gamma failed.");
        Assert.Contains(host.Faults, fault => fault.Message == "The entry assembly was not found.");
        Assert.Contains(host.Faults, fault => fault.Message == "The entry type does not implement IMacroPlugin.");
        Assert.Contains(host.Faults, fault => fault.Message == "The plugin id does not match the manifest.");
        Assert.DoesNotContain(ProbeLog.Stops, stop => stop == "com.clickstitch.gamma");
        host.Publish(PluginEventKind.MacroStarted);
        Assert.DoesNotContain("gamma", ProbeLog.Events);
    }

    [Fact]
    public void Registration_closes_when_initialize_returns_and_rejects_a_duplicate()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-dup", "com.clickstitch.duplicate", "1.0", "DuplicateActionPlugin");
        WritePlugin("c-late", "com.clickstitch.epsilon", "1.0", "EpsilonPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));

        Assert.Equal(["com.clickstitch.alpha", "com.clickstitch.epsilon"], host.LoadedIds);
        Assert.Contains(host.Faults, fault => fault.Message == "Plugin initialization failed. Action id alpha.ping is already registered.");
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ProbeLog.LateHost!.RegisterAction(new PluginActionDescriptor("late.ping", "Late", "Too late.", "{}"), _ => { }));
        Assert.Equal("Actions can only be registered during Initialize.", error.Message);
        ICoreHost late = ProbeLog.LateHost ?? throw new InvalidOperationException("The plugin did not keep the host.");
        PluginEventHandler started = ProbeLog.StartedHandler ?? throw new InvalidOperationException("The plugin did not keep the handler.");
        host.Publish(PluginEventKind.MacroStarted);
        Assert.Equal(1, ProbeLog.Started);
        late.Unsubscribe(PluginEventKind.MacroStarted, started);
        host.Publish(PluginEventKind.MacroStarted);
        Assert.Equal(1, ProbeLog.Started);
        Assert.Throws<InvalidOperationException>(() => late.Subscribe(PluginEventKind.MacroStarted, started));
    }

    [Fact]
    public void A_failed_action_is_reported_and_releases_owned_inputs()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-boom", "com.clickstitch.boom", "1.0", "BoomPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));
        int releases = 0;
        host.ReleaseOwnedInputs = () => releases++;

        PluginCallResult failed = host.Execute("alpha.boom", "{}", CancellationToken.None);
        PluginCallResult next = host.Execute("alpha.ping", "{}");

        Assert.False(failed.Succeeded);
        Assert.Equal("Plugin action failed. Boom.", failed.Fault!.Message);
        Assert.Equal(1, releases);
        Assert.True(next.Succeeded);
        Assert.Equal("pong", next.Detail);
        Assert.False(host.Execute("alpha.ping", "[]").Succeeded);
    }

    [Fact]
    public void One_event_failure_does_not_stop_the_next_handler()
    {
        WritePlugin("a-bad", "com.clickstitch.badevent", "1.0", "BadEventPlugin");
        WritePlugin("b-good", "com.clickstitch.events", "1.0", "EventPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));
        int releases = 0;
        host.ReleaseOwnedInputs = () => releases++;

        host.Publish(PluginEventKind.MacroStarted);

        Assert.Contains(host.Faults, fault => fault.Message == "Plugin event failed. Event failed.");
        Assert.Equal(["MacroStarted"], ProbeLog.Events);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void Shutdown_continues_after_one_plugin_throws()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-fragile", "com.clickstitch.fragile", "1.0", "FragilePlugin");
        WritePlugin("c-beta", "com.clickstitch.beta", "1.0", "BetaPlugin");
        using PluginHost host = PluginHost.Load(PluginDiscovery.Discover(_root));

        host.Shutdown();

        Assert.Equal(["com.clickstitch.beta", "com.clickstitch.alpha"], ProbeLog.Stops);
        Assert.Contains(host.Faults, fault => fault.Message == "Plugin shutdown failed. Fragile shutdown.");
    }

    [Fact]
    public void A_disabled_plugin_stays_unloaded_until_the_warning_is_accepted()
    {
        WritePlugin("a-alpha", "com.clickstitch.alpha", "1.0", "AlphaPlugin");
        WritePlugin("b-beta", "com.clickstitch.beta", "1.0", "BetaPlugin");
        PluginCatalog catalog = PluginDiscovery.Discover(_root);
        PluginSettings enabled = PluginSettings.Off.Acknowledge().Enable("com.clickstitch.alpha");
        string path = Path.Combine(_root, "settings.json");
        PluginSettingsStore.Write(path, enabled);
        File.WriteAllText(Path.Combine(_root, "unwarned.json"), """
            {
              "thirdPartyPluginsAcknowledged": false,
              "enabledPluginIds": ["com.clickstitch.alpha"]
            }
            """);
        File.WriteAllText(Path.Combine(_root, "broken.json"), "{ \"extra\": true }");

        Assert.Throws<InvalidOperationException>(() => PluginSettings.Off.Enable("com.clickstitch.alpha"));
        PluginSettingsLoad unwarned = PluginSettingsStore.Read(Path.Combine(_root, "unwarned.json"));
        PluginSettingsLoad broken = PluginSettingsStore.Read(Path.Combine(_root, "broken.json"));
        using PluginHost blocked = PluginHost.LoadEnabled(catalog, unwarned.Settings);
        using PluginHost host = PluginHost.LoadEnabled(catalog, PluginSettingsStore.Read(path).Settings);

        Assert.False(broken.Settings.ThirdPartyPluginsAcknowledged);
        Assert.NotNull(broken.Fault);

        Assert.Empty(blocked.LoadedIds);
        Assert.Contains(blocked.Faults, fault => fault.Message == "Plugins stay off until the trust warning is accepted.");
        Assert.Equal(["com.clickstitch.alpha"], host.LoadedIds);
        Assert.EndsWith(Path.Combine("ClickStitch", "settings.json"), PluginSettingsStore.DefaultPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_sample_plugin_registers_and_runs_its_action()
    {
        string directory = Path.Combine(_root, "sample");
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "ClickStitch.SamplePlugin.dll"), Path.Combine(directory, "ClickStitch.SamplePlugin.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "clickstitch-plugin.json"), Path.Combine(directory, "clickstitch-plugin.json"));
        PluginSettings settings = PluginSettings.Off.Acknowledge().Enable("com.clickstitch.sample");
        using PluginHost host = PluginHost.LoadEnabled(PluginDiscovery.Discover(_root), settings);

        PluginCallResult result = host.Execute("sample.ping", "{}");

        Assert.Equal(["com.clickstitch.sample"], host.LoadedIds);
        Assert.True(result.Succeeded);
        Assert.Equal("Sample action ran.", result.Detail);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.True(host.Execute("sample.ping", "{}", cancelled.Token).Cancelled);
    }

    [Fact]
    public void The_manager_lists_version_compatibility_and_the_next_start()
    {
        WritePlugin("alpha", "com.clickstitch.alpha", "1.0", "Alpha");
        WritePlugin("old", "com.clickstitch.old", "2.0", "Alpha");
        PluginCatalog catalog = PluginDiscovery.Discover(_root);
        PluginSettings settings = PluginSettings.Off.Acknowledge().Enable("com.clickstitch.alpha");

        PluginListing listing = Assert.Single(PluginManager.List(catalog, settings, ["com.clickstitch.alpha"]));
        Assert.Equal("com.clickstitch.alpha", listing.Id);
        Assert.Equal("1.0.0", listing.Version);
        Assert.Equal("1.0", listing.ApiVersion);
        Assert.True(listing.Compatible);
        Assert.True(listing.Enabled);
        Assert.True(listing.Loaded);
        Assert.Equal("Loaded.", listing.State);

        PluginListing waiting = Assert.Single(PluginManager.List(catalog, settings, []));
        Assert.Equal("On. It loads the next time ClickStitch starts.", waiting.State);

        File.WriteAllText(Path.Combine(_root, "untrusted.json"), """
            {
              "thirdPartyPluginsAcknowledged": false,
              "enabledPluginIds": [ "com.clickstitch.alpha" ]
            }
            """);
        PluginSettings untrusted = PluginSettingsStore.Read(Path.Combine(_root, "untrusted.json")).Settings;
        PluginListing blocked = Assert.Single(PluginManager.List(catalog, untrusted, []));
        Assert.Contains("warning", blocked.State, StringComparison.Ordinal);

        Assert.Contains(catalog.Faults, fault => fault.Message.Contains("2.0", StringComparison.Ordinal));
        IReadOnlyList<PluginFault> errors = PluginManager.Errors(catalog, catalog.Faults);
        Assert.Equal(catalog.Faults.Count, errors.Count);
    }

    [Fact]
    public void Appearance_is_saved_and_a_bad_value_is_ignored()
    {
        string path = Path.Combine(_root, "theme.json");
        PluginSettingsStore.Write(path, PluginSettings.Off.Acknowledge().WithAppearance(AppAppearance.Dark));
        PluginSettings loaded = PluginSettingsStore.Read(path).Settings;
        Assert.Equal(AppAppearance.Dark, loaded.Appearance);
        Assert.True(loaded.ThirdPartyPluginsAcknowledged);

        File.WriteAllText(Path.Combine(_root, "plain.json"), """
            {
              "thirdPartyPluginsAcknowledged": false,
              "enabledPluginIds": []
            }
            """);
        Assert.Equal(AppAppearance.System, PluginSettingsStore.Read(Path.Combine(_root, "plain.json")).Settings.Appearance);

        File.WriteAllText(Path.Combine(_root, "neon.json"), """
            {
              "thirdPartyPluginsAcknowledged": false,
              "enabledPluginIds": [],
              "appearance": "neon"
            }
            """);
        PluginSettingsLoad neon = PluginSettingsStore.Read(Path.Combine(_root, "neon.json"));
        Assert.False(neon.Settings.ThirdPartyPluginsAcknowledged);
        Assert.Equal("appearance must be light, dark, or system.", neon.Fault!.Message);
        Assert.Equal(AppAppearance.System, PluginSettings.Off.RevokeTrust().Appearance);
    }

    private void WritePlugin(string folder, string id, string api, string type, bool copyAssembly = true)
    {
        string directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        if (copyAssembly)
        {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "ClickStitch.Plugins.Probe.dll"),
                Path.Combine(directory, "ClickStitch.Plugins.Probe.dll"),
                overwrite: true);
        }

        File.WriteAllText(Path.Combine(directory, PluginManifests.FileName), $$"""
            {
              "id": "{{id}}",
              "name": "Probe",
              "version": "1.0.0",
              "apiVersion": "{{api}}",
              "entryAssembly": "ClickStitch.Plugins.Probe.dll",
              "entryType": "ClickStitch.Plugins.Probe.{{type}}"
            }
            """);
    }
}
