namespace ClickStitch.Plugins;

/// <summary>One installed plugin as the manager shows it. This does not load the assembly.</summary>
public sealed class PluginListing
{
    internal PluginListing(string id, string name, string version, string apiVersion, bool compatible, bool enabled, bool loaded, string state)
    {
        Id = id;
        Name = name;
        Version = version;
        ApiVersion = apiVersion;
        Compatible = compatible;
        Enabled = enabled;
        Loaded = loaded;
        State = state;
    }

    public string Id { get; }

    public string Name { get; }

    public string Version { get; }

    public string ApiVersion { get; }

    public bool Compatible { get; }

    public bool Enabled { get; }

    public bool Loaded { get; }

    public string State { get; }
}

/// <summary>Turns a discovery catalog into the plugin manager list. It does not load or run plugins.</summary>
public static class PluginManager
{
    public static IReadOnlyList<PluginListing> List(PluginCatalog catalog, PluginSettings settings, IReadOnlyList<string> loadedIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loadedIds);
        var listings = new List<PluginListing>(catalog.Plugins.Count);
        foreach (DiscoveredPlugin plugin in catalog.Plugins)
        {
            PluginMetadata metadata = plugin.Manifest.Metadata;
            bool compatible = metadata.ApiVersion.IsSupportedBy(PluginApi.Current);
            bool enabled = settings.EnabledIds.Contains(metadata.Id, StringComparer.Ordinal);
            bool loaded = loadedIds.Contains(metadata.Id, StringComparer.Ordinal);
            listings.Add(new PluginListing(
                metadata.Id,
                metadata.Name,
                metadata.Version.ToString(),
                metadata.ApiVersion.ToString(),
                compatible,
                enabled,
                loaded,
                State(compatible, enabled, loaded, settings.ThirdPartyPluginsAcknowledged)));
        }

        return listings;
    }

    public static IReadOnlyList<PluginFault> Errors(PluginCatalog catalog, IReadOnlyList<PluginFault> runtimeFaults)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runtimeFaults);
        var errors = new List<PluginFault>(catalog.Faults);
        foreach (PluginFault fault in runtimeFaults)
        {
            bool seen = false;
            foreach (PluginFault existing in errors)
            {
                if (existing.Source == fault.Source && existing.Message == fault.Message)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                errors.Add(fault);
            }
        }

        return errors;
    }

    private static string State(bool compatible, bool enabled, bool loaded, bool acknowledged)
    {
        if (!compatible)
        {
            return "Not compatible with this version of ClickStitch.";
        }

        if (!acknowledged)
        {
            return enabled
                ? "Chosen, and it stays off until the warning is accepted."
                : "Off.";
        }

        if (loaded)
        {
            return "Loaded.";
        }

        return enabled ? "On. It loads the next time ClickStitch starts." : "Off.";
    }
}
