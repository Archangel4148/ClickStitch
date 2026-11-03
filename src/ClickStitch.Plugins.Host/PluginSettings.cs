using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClickStitch.Plugins;

/// <summary>How the shell paints. System follows the Windows app theme.</summary>
public enum AppAppearance
{
    System,
    Light,
    Dark,
}

/// <summary>Which plugins may load, and how the shell looks. Enabling a plugin requires the trust warning.</summary>
public sealed class PluginSettings
{
    public static PluginSettings Off { get; } = new(false, [], AppAppearance.System);

    private PluginSettings(bool thirdPartyPluginsAcknowledged, IReadOnlyList<string> enabledIds, AppAppearance appearance)
    {
        if (!Enum.IsDefined(appearance))
        {
            throw new ArgumentOutOfRangeException(nameof(appearance));
        }

        ThirdPartyPluginsAcknowledged = thirdPartyPluginsAcknowledged;
        EnabledIds = enabledIds;
        Appearance = appearance;
    }

    public bool ThirdPartyPluginsAcknowledged { get; }

    public IReadOnlyList<string> EnabledIds { get; }

    public AppAppearance Appearance { get; }

    public PluginSettings Acknowledge() => new(true, EnabledIds, Appearance);

    public PluginSettings RevokeTrust() => new(false, EnabledIds, Appearance);

    public PluginSettings WithAppearance(AppAppearance appearance) => new(ThirdPartyPluginsAcknowledged, EnabledIds, appearance);

    public PluginSettings Enable(string pluginId)
    {
        if (!ThirdPartyPluginsAcknowledged)
        {
            throw new InvalidOperationException("Accept the plugin warning before enabling a plugin.");
        }

        if (!PluginManifests.IsPluginId(pluginId))
        {
            throw new ArgumentException("Id must be 1–100 characters of reverse-DNS labels, such as com.example.sample.", nameof(pluginId));
        }

        if (EnabledIds.Contains(pluginId, StringComparer.Ordinal))
        {
            return this;
        }

        var ids = new List<string>(EnabledIds) { pluginId };
        return new PluginSettings(true, ids, Appearance);
    }

    public PluginSettings Disable(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        if (!EnabledIds.Contains(pluginId, StringComparer.Ordinal))
        {
            return this;
        }

        return new PluginSettings(ThirdPartyPluginsAcknowledged, EnabledIds.Where(id => id != pluginId).ToArray(), Appearance);
    }

    internal static PluginSettings FromFile(bool acknowledged, IReadOnlyList<string> enabledIds, AppAppearance appearance) =>
        new(acknowledged, enabledIds, appearance);
}

/// <summary>A settings read. A fault means the file was ignored and plugins stay off.</summary>
public sealed class PluginSettingsLoad
{
    internal PluginSettingsLoad(PluginSettings settings, PluginFault? fault)
    {
        Settings = settings;
        Fault = fault;
    }

    public PluginSettings Settings { get; }

    public PluginFault? Fault { get; }
}

/// <summary>Reads and writes the local plugin switch. A missing file leaves plugins off and creates nothing.</summary>
public static class PluginSettingsStore
{
    public const int MaxDocumentBytes = 64 * 1024;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
        MaxDepth = 8,
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickStitch", "settings.json");

    public static PluginSettingsLoad Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new PluginSettingsLoad(PluginSettings.Off, null);
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxDocumentBytes)
            {
                return Ignored("The settings file is larger than 64 KiB.");
            }

            return Parse(File.ReadAllBytes(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return Ignored("The settings file could not be read. " + error.Message);
        }
    }

    public static void Write(string path, PluginSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        string fullPath = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("thirdPartyPluginsAcknowledged", settings.ThirdPartyPluginsAcknowledged);
            writer.WriteString("appearance", AppearanceName(settings.Appearance));
            writer.WriteStartArray("enabledPluginIds");
            foreach (string id in settings.EnabledIds)
            {
                writer.WriteStringValue(id);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        File.WriteAllText(fullPath, Encoding.UTF8.GetString(stream.ToArray()), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static PluginSettingsLoad Parse(byte[] utf8)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(utf8, documentOptions: DocumentOptions);
        }
        catch (JsonException error)
        {
            return Ignored("The settings file is not valid JSON. " + error.Message);
        }

        if (root is not JsonObject document)
        {
            return Ignored("The settings file must be a JSON object.");
        }

        foreach (string name in document.Select(member => member.Key))
        {
            if (name is not "thirdPartyPluginsAcknowledged" and not "enabledPluginIds" and not "appearance")
            {
                return Ignored("Unknown settings member '" + name + "'.");
            }
        }

        if (document["thirdPartyPluginsAcknowledged"] is not JsonValue acknowledgedValue || !acknowledgedValue.TryGetValue(out bool acknowledged))
        {
            return Ignored("thirdPartyPluginsAcknowledged must be true or false.");
        }

        if (document["enabledPluginIds"] is not JsonArray ids)
        {
            return Ignored("enabledPluginIds must be an array.");
        }

        var enabled = new List<string>();
        foreach (JsonNode? item in ids)
        {
            if (item is not JsonValue value || !value.TryGetValue(out string? id) || id is null || !PluginManifests.IsPluginId(id))
            {
                return Ignored("enabledPluginIds must contain plugin ids.");
            }

            if (enabled.Contains(id, StringComparer.Ordinal))
            {
                return Ignored("Plugin id " + id + " is listed twice.");
            }

            enabled.Add(id);
        }

        AppAppearance appearance = AppAppearance.System;
        if (document.ContainsKey("appearance"))
        {
            if (document["appearance"] is not JsonValue appearanceValue
                || !appearanceValue.TryGetValue(out string? appearanceText)
                || !TryAppearance(appearanceText, out appearance))
            {
                return Ignored("appearance must be light, dark, or system.");
            }
        }

        return new PluginSettingsLoad(PluginSettings.FromFile(acknowledged, enabled, appearance), null);
    }

    private static string AppearanceName(AppAppearance appearance) => appearance switch
    {
        AppAppearance.Light => "light",
        AppAppearance.Dark => "dark",
        _ => "system",
    };

    private static bool TryAppearance(string? text, out AppAppearance appearance)
    {
        switch (text)
        {
            case "light":
                appearance = AppAppearance.Light;
                return true;
            case "dark":
                appearance = AppAppearance.Dark;
                return true;
            case "system":
                appearance = AppAppearance.System;
                return true;
            default:
                appearance = AppAppearance.System;
                return false;
        }
    }

    private static PluginSettingsLoad Ignored(string message) =>
        new(PluginSettings.Off, new PluginFault("settings", message));
}
