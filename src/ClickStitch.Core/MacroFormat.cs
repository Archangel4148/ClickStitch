namespace ClickStitch.Core;

/// <summary>
/// Macro document format recognized by this build.
/// Checking the version does not parse, validate, or run a macro.
/// </summary>
public static class MacroFormat
{
    /// <summary>
    /// Format version defined by the Phase 0 schema and accepted by <see cref="IsSupported"/>.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Reports whether a document header version is one this build is allowed to read.
    /// </summary>
    /// <param name="formatVersion">The document's formatVersion value.</param>
    /// <returns>True only for <see cref="CurrentVersion"/>.</returns>
    public static bool IsSupported(int formatVersion) => formatVersion == CurrentVersion;
}
