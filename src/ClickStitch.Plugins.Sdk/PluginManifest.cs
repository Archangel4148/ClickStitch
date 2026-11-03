using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClickStitch.Plugins;

/// <summary>The identity fields a manifest must name.</summary>
public sealed class PluginMetadata
{
    public PluginMetadata(string id, string name, PluginVersion version, PluginApiVersion apiVersion)
    {
        if (!PluginManifests.IsPluginId(id))
        {
            throw new ArgumentException("Id must be 1–100 characters of reverse-DNS labels, such as com.example.sample.", nameof(id));
        }

        if (!PluginManifests.IsName(name))
        {
            throw new ArgumentException("Name must be 1–120 characters and must not start or end with whitespace.", nameof(name));
        }

        if (!IsComponent(version.Major) || !IsComponent(version.Minor) || !IsComponent(version.Patch))
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be major.minor.patch, such as 1.0.0.");
        }

        if (!IsComponent(apiVersion.Major) || !IsComponent(apiVersion.Minor))
        {
            throw new ArgumentOutOfRangeException(nameof(apiVersion), "API version must be major.minor, such as 1.0.");
        }

        Id = id;
        Name = name;
        Version = version;
        ApiVersion = apiVersion;
    }

    private static bool IsComponent(int value) => value is >= 0 and <= 999_999_999;

    public string Id { get; }

    public string Name { get; }

    public PluginVersion Version { get; }

    public PluginApiVersion ApiVersion { get; }
}

/// <summary>
/// One validated <c>clickstitch-plugin.json</c>. Reading it does not load the assembly or look for a Plugins directory.
/// </summary>
public sealed class PluginManifest
{
    internal PluginManifest(PluginMetadata metadata, string entryAssembly, string entryType)
    {
        Metadata = metadata;
        EntryAssembly = entryAssembly;
        EntryType = entryType;
    }

    public PluginMetadata Metadata { get; }

    public string EntryAssembly { get; }

    public string EntryType { get; }
}

/// <summary>One manifest failure. <see cref="Path"/> names the field, such as <c>apiVersion</c>.</summary>
public sealed class PluginIssue
{
    internal PluginIssue(string path, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Path = path;
        Message = message;
    }

    public string Path { get; }

    public string Message { get; }
}

/// <summary>The outcome of reading a plugin manifest. A valid result has a manifest and no issues.</summary>
public sealed class PluginReadResult
{
    internal PluginReadResult(PluginManifest? manifest, IReadOnlyList<PluginIssue> issues)
    {
        Manifest = manifest;
        Issues = issues;
    }

    public PluginManifest? Manifest { get; }

    public IReadOnlyList<PluginIssue> Issues { get; }

    public bool IsValid => Manifest is not null;
}

/// <summary>Reads and checks a plugin manifest. It does not open the entry assembly.</summary>
public static class PluginManifests
{
    public const string FileName = "clickstitch-plugin.json";

    public const int MaxDocumentBytes = 64 * 1024;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
        MaxDepth = 16,
    };

    private static readonly HashSet<string> KnownMembers = new(StringComparer.Ordinal)
    {
        "id", "name", "version", "apiVersion", "entryAssembly", "entryType",
    };

    public static PluginReadResult Read(string json) => Read(json, PluginApi.Current);

    public static PluginReadResult Read(string json, PluginApiVersion host)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxDocumentBytes)
        {
            return TooLarge();
        }

        return Read(Encoding.UTF8.GetBytes(json), host);
    }

    public static PluginReadResult Read(ReadOnlySpan<byte> utf8) => Read(utf8, PluginApi.Current);

    public static PluginReadResult Read(ReadOnlySpan<byte> utf8, PluginApiVersion host)
    {
        if (utf8.Length > MaxDocumentBytes)
        {
            return TooLarge();
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(utf8, documentOptions: DocumentOptions);
        }
        catch (JsonException error)
        {
            return Fail("manifest", "The manifest is not valid JSON. " + error.Message);
        }

        if (root is not JsonObject document)
        {
            return Fail("manifest", "The manifest must be a JSON object.");
        }

        var issues = new List<PluginIssue>();
        foreach (KeyValuePair<string, JsonNode?> member in document)
        {
            if (!KnownMembers.Contains(member.Key))
            {
                issues.Add(new PluginIssue(member.Key, "Unknown member '" + member.Key + "'."));
            }
        }

        string? id = ReadText(document, "id", issues);
        string? name = ReadText(document, "name", issues);
        string? versionText = ReadText(document, "version", issues);
        string? apiText = ReadText(document, "apiVersion", issues);
        string? entryAssembly = ReadText(document, "entryAssembly", issues);
        string? entryType = ReadText(document, "entryType", issues);

        if (id is not null && !IsPluginId(id))
        {
            issues.Add(new PluginIssue("id", "Id must be 1–100 characters of reverse-DNS labels, such as com.example.sample."));
        }

        if (name is not null && !IsName(name))
        {
            issues.Add(new PluginIssue("name", "Name must be 1–120 characters and must not start or end with whitespace."));
        }

        PluginVersion version = default;
        if (versionText is not null && !TryVersion(versionText, out version))
        {
            issues.Add(new PluginIssue("version", "Version must be major.minor.patch, such as 1.0.0."));
        }

        PluginApiVersion api = default;
        if (apiText is not null && !TryApiVersion(apiText, out api))
        {
            issues.Add(new PluginIssue("apiVersion", "API version must be major.minor, such as 1.0."));
        }
        else if (apiText is not null && !api.IsSupportedBy(host))
        {
            issues.Add(new PluginIssue(
                "apiVersion",
                $"Plugin API {api} is not compatible with host API {host}. The major number must match, and the plugin minor must not be newer than the host."));
        }

        if (entryAssembly is not null && !IsEntryAssembly(entryAssembly))
        {
            issues.Add(new PluginIssue("entryAssembly", "Entry assembly must be a .dll file name in the plugin directory. A path is not allowed."));
        }

        if (entryType is not null && !IsEntryType(entryType))
        {
            issues.Add(new PluginIssue("entryType", "Entry type must be a type name, such as Sample.Plugin."));
        }

        if (issues.Count > 0 || id is null || name is null || versionText is null || apiText is null || entryAssembly is null || entryType is null)
        {
            return new PluginReadResult(null, issues);
        }

        var metadata = new PluginMetadata(id, name, version, api);
        return new PluginReadResult(new PluginManifest(metadata, entryAssembly, entryType), []);
    }

    /// <summary>Reports ids that appear more than once. The manifests themselves are not changed.</summary>
    public static IReadOnlyList<PluginIssue> DuplicateIds(IEnumerable<PluginManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<PluginIssue>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (PluginManifest manifest in manifests)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            string id = manifest.Metadata.Id;
            if (!seen.Add(id) && reported.Add(id))
            {
                duplicates.Add(new PluginIssue("id", "Plugin id " + id + " is already used."));
            }
        }

        return duplicates;
    }

    private static string? ReadText(JsonObject document, string name, List<PluginIssue> issues)
    {
        if (!document.ContainsKey(name))
        {
            issues.Add(new PluginIssue(name, NameOf(name) + " is required."));
            return null;
        }

        if (document[name] is not JsonValue value || !value.TryGetValue(out string? text) || text is null)
        {
            issues.Add(new PluginIssue(name, NameOf(name) + " must be text."));
            return null;
        }

        return text;
    }

    private static string NameOf(string field) => field switch
    {
        "id" => "Id",
        "name" => "Name",
        "version" => "Version",
        "apiVersion" => "API version",
        "entryAssembly" => "Entry assembly",
        "entryType" => "Entry type",
        _ => field,
    };

    internal static bool IsPluginId(string id)
    {
        if (id is null || id.Length is < 1 or > 100)
        {
            return false;
        }

        string[] labels = id.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (string label in labels)
        {
            if (label.Length == 0 || !char.IsAsciiLetter(label[0]) || label[^1] == '-')
            {
                return false;
            }

            foreach (char character in label)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static bool IsName(string name) =>
        name is not null && name.Length is >= 1 and <= 120 && string.Equals(name, name.Trim(), StringComparison.Ordinal) && name.All(character => !char.IsControl(character));

    private static bool TryVersion(string text, out PluginVersion version)
    {
        version = default;
        string[] parts = text.Split('.');
        if (parts.Length != 3 || !TryComponent(parts[0], out int major) || !TryComponent(parts[1], out int minor) || !TryComponent(parts[2], out int patch))
        {
            return false;
        }

        version = new PluginVersion(major, minor, patch);
        return true;
    }

    private static bool TryApiVersion(string text, out PluginApiVersion version)
    {
        version = default;
        string[] parts = text.Split('.');
        if (parts.Length != 2 || !TryComponent(parts[0], out int major) || !TryComponent(parts[1], out int minor))
        {
            return false;
        }

        version = new PluginApiVersion(major, minor);
        return true;
    }

    private static bool TryComponent(string text, out int value)
    {
        value = 0;
        if (text.Length is < 1 or > 9 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(text, out value);
    }

    private static bool IsEntryAssembly(string name)
    {
        if (name.Length is < 5 or > 120 || name != Path.GetFileName(name))
        {
            return false;
        }

        if (name.Contains('\\') || name.Contains('/') || name.Contains(':') || name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string stem = name[..^4];
        if (stem.Length == 0)
        {
            return false;
        }

        foreach (char character in stem)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEntryType(string name)
    {
        if (name.Length is < 1 or > 200)
        {
            return false;
        }

        string[] parts = name.Split('.');
        foreach (string part in parts)
        {
            if (part.Length == 0 || (!char.IsAsciiLetter(part[0]) && part[0] != '_'))
            {
                return false;
            }

            foreach (char character in part)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static PluginReadResult TooLarge() =>
        Fail("manifest", "The manifest is larger than 64 KiB.");

    private static PluginReadResult Fail(string path, string message) =>
        new(null, [new PluginIssue(path, message)]);
}
