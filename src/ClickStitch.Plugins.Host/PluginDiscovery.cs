namespace ClickStitch.Plugins;

/// <summary>One plugin folder whose manifest passed validation, including the API check. The assembly is not loaded.</summary>
public sealed class DiscoveredPlugin
{
    internal DiscoveredPlugin(string directory, PluginManifest manifest)
    {
        Directory = directory;
        Manifest = manifest;
    }

    public string Directory { get; }

    public PluginManifest Manifest { get; }
}

/// <summary>Plugin folders that can be loaded, and the folders that were skipped.</summary>
public sealed class PluginCatalog
{
    public static PluginCatalog Empty { get; } = new([], []);

    internal PluginCatalog(IReadOnlyList<DiscoveredPlugin> plugins, IReadOnlyList<PluginFault> faults)
    {
        Plugins = plugins;
        Faults = faults;
    }

    public IReadOnlyList<DiscoveredPlugin> Plugins { get; }

    public IReadOnlyList<PluginFault> Faults { get; }
}

/// <summary>
/// Reads <c>Plugins</c> subfolders. A missing or empty directory is an empty catalog.
/// This does not load plugin code.
/// </summary>
public static class PluginDiscovery
{
    public const string DirectoryName = "Plugins";

    public static PluginCatalog Discover(string pluginsDirectory) => Discover(pluginsDirectory, PluginApi.Current);

    public static PluginCatalog Discover(string pluginsDirectory, PluginApiVersion host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);
        if (!Directory.Exists(pluginsDirectory))
        {
            return PluginCatalog.Empty;
        }

        string[] directories;
        try
        {
            directories = Directory.EnumerateDirectories(pluginsDirectory)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new PluginCatalog([], [new PluginFault(DirectoryName, "The Plugins folder could not be read. " + error.Message)]);
        }

        var plugins = new List<DiscoveredPlugin>();
        var faults = new List<PluginFault>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string directory in directories)
        {
            string name = Path.GetFileName(directory);
            string fullDirectory = Path.GetFullPath(directory);
            if (!IsInside(pluginsDirectory, fullDirectory))
            {
                faults.Add(new PluginFault(name, "The plugin directory escapes the Plugins folder."));
                continue;
            }

            string manifestPath = Path.Combine(fullDirectory, PluginManifests.FileName);
            if (!File.Exists(manifestPath) || !IsInside(fullDirectory, Path.GetFullPath(manifestPath)))
            {
                faults.Add(new PluginFault(name, "The plugin manifest was not found."));
                continue;
            }

            long length;
            try
            {
                length = new FileInfo(manifestPath).Length;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                faults.Add(new PluginFault(name, "The plugin manifest could not be read. " + error.Message));
                continue;
            }

            if (length > PluginManifests.MaxDocumentBytes)
            {
                faults.Add(new PluginFault(name, "The manifest is larger than 64 KiB."));
                continue;
            }

            PluginReadResult read;
            try
            {
                read = PluginManifests.Read(File.ReadAllBytes(manifestPath), host);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                faults.Add(new PluginFault(name, "The plugin manifest could not be read. " + error.Message));
                continue;
            }

            if (!read.IsValid || read.Manifest is null)
            {
                faults.Add(new PluginFault(name, string.Join(" ", read.Issues.Select(issue => issue.Path + ": " + issue.Message))));
                continue;
            }

            string id = read.Manifest.Metadata.Id;
            if (!seen.Add(id))
            {
                faults.Add(new PluginFault(id, "Plugin id " + id + " is already used."));
                continue;
            }

            plugins.Add(new DiscoveredPlugin(fullDirectory, read.Manifest));
        }

        return new PluginCatalog(plugins, faults);
    }

    internal static bool IsInside(string root, string candidate)
    {
        string fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar) && !fullRoot.EndsWith(Path.AltDirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        return Path.GetFullPath(candidate).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }
}
