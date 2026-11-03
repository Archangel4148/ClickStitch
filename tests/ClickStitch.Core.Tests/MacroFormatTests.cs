namespace ClickStitch.Core.Tests;

public sealed class MacroFormatTests
{
    [Fact]
    public void Current_version_matches_the_phase_0_schema()
    {
        Assert.Equal(1, MacroFormat.CurrentVersion);
        Assert.True(MacroFormat.IsSupported(MacroFormat.CurrentVersion));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Unknown_versions_are_rejected(int formatVersion)
    {
        Assert.False(MacroFormat.IsSupported(formatVersion));
    }
}
