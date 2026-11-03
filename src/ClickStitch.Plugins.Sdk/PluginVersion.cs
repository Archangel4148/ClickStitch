namespace ClickStitch.Plugins;

/// <summary>A plugin package version, <c>major.minor.patch</c>.</summary>
public readonly record struct PluginVersion(int Major, int Minor, int Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>
/// The plugin API version, <c>major.minor</c>. It is not the macro format version.
/// A plugin is compatible when the major matches the host and its minor is not newer than the host.
/// </summary>
public readonly record struct PluginApiVersion(int Major, int Minor)
{
    public bool IsSupportedBy(PluginApiVersion host) => Major == host.Major && Minor <= host.Minor;

    public override string ToString() => $"{Major}.{Minor}";
}

/// <summary>The API version this build of ClickStitch provides to plugins.</summary>
public static class PluginApi
{
    public static PluginApiVersion Current { get; } = new(1, 0);
}
