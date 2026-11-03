namespace ClickStitch.Plugins.Tests;

public sealed class PluginManifestTests
{
    private const string Valid = """
        {
          "id": "com.example.sample",
          "name": "Sample",
          "version": "1.0.0",
          "apiVersion": "1.0",
          "entryAssembly": "Sample.dll",
          "entryType": "Sample.Plugin"
        }
        """;

    [Fact]
    public void A_current_manifest_is_accepted_and_does_not_open_an_assembly()
    {
        PluginReadResult result = PluginManifests.Read(Valid);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal("com.example.sample", result.Manifest!.Metadata.Id);
        Assert.Equal("Sample", result.Manifest.Metadata.Name);
        Assert.Equal(new PluginVersion(1, 0, 0), result.Manifest.Metadata.Version);
        Assert.Equal(PluginApi.Current, result.Manifest.Metadata.ApiVersion);
        Assert.Equal("Sample.dll", result.Manifest.EntryAssembly);
        Assert.Equal("Sample.Plugin", result.Manifest.EntryType);
        Assert.Equal("clickstitch-plugin.json", PluginManifests.FileName);
    }

    [Fact]
    public void An_older_minor_is_compatible_and_a_newer_one_is_not()
    {
        PluginReadResult older = PluginManifests.Read(With("apiVersion", "1.0"), new PluginApiVersion(1, 2));
        PluginReadResult newer = PluginManifests.Read(With("apiVersion", "1.1"));
        PluginReadResult otherMajor = PluginManifests.Read(With("apiVersion", "2.0"));

        Assert.True(older.IsValid);
        Assert.False(newer.IsValid);
        Assert.Equal(
            "Plugin API 1.1 is not compatible with host API 1.0. The major number must match, and the plugin minor must not be newer than the host.",
            newer.Issues[0].Message);
        Assert.False(otherMajor.IsValid);
        Assert.Contains("2.0", otherMajor.Issues[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_and_a_non_dll_are_rejected()
    {
        AssertInvalid(With("entryAssembly", "../Sample.dll"), "entryAssembly");
        AssertInvalid(With("entryAssembly", "sub/Sample.dll"), "entryAssembly");
        AssertInvalid(With("entryAssembly", "Sample.exe"), "entryAssembly");
        AssertInvalid(With("id", "Sample"), "id");
        AssertInvalid(With("version", "1.0"), "version");
        AssertInvalid(With("version", "01.0.0"), "version");
        AssertInvalid(With("apiVersion", "1"), "apiVersion");
        AssertInvalid(With("entryType", "Sample."), "entryType");
        AssertInvalid(With("name", " Sample"), "name");
    }

    [Fact]
    public void A_missing_field_and_an_unknown_member_are_errors()
    {
        PluginReadResult missing = PluginManifests.Read("""{ "id": "com.example.sample" }""");
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Issues, issue => issue.Path == "name" && issue.Message == "Name is required.");

        PluginReadResult extra = PluginManifests.Read(Valid.TrimEnd()[..^1] + ", \"extra\": true }");
        Assert.False(extra.IsValid);
        Assert.Contains(extra.Issues, issue => issue.Message == "Unknown member 'extra'.");
    }

    [Fact]
    public void Duplicate_ids_are_reported_once()
    {
        PluginManifest first = PluginManifests.Read(Valid).Manifest!;
        PluginManifest second = PluginManifests.Read(With("name", "Other")).Manifest!;
        PluginManifest third = PluginManifests.Read(With("id", "com.example.other")).Manifest!;

        IReadOnlyList<PluginIssue> duplicates = PluginManifests.DuplicateIds([first, second, first, third]);

        PluginIssue issue = Assert.Single(duplicates);
        Assert.Equal("Plugin id com.example.sample is already used.", issue.Message);
    }

    [Fact]
    public void A_manifest_over_64_kib_is_rejected()
    {
        PluginReadResult result = PluginManifests.Read(new string(' ', PluginManifests.MaxDocumentBytes + 1));

        Assert.False(result.IsValid);
        Assert.Equal("The manifest is larger than 64 KiB.", result.Issues[0].Message);
    }

    private static void AssertInvalid(string json, string path)
    {
        PluginReadResult result = PluginManifests.Read(json);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Path == path);
    }

    private static string With(string field, string value) => Valid.Replace(Field(field), "\"" + field + "\": \"" + value + "\"", StringComparison.Ordinal);

    private static string Field(string field) => field switch
    {
        "id" => "\"id\": \"com.example.sample\"",
        "name" => "\"name\": \"Sample\"",
        "version" => "\"version\": \"1.0.0\"",
        "apiVersion" => "\"apiVersion\": \"1.0\"",
        "entryAssembly" => "\"entryAssembly\": \"Sample.dll\"",
        "entryType" => "\"entryType\": \"Sample.Plugin\"",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
