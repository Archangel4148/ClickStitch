using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class DesktopTryoutTests
{
    [Fact]
    public void The_tryout_documents_are_ready_to_run()
    {
        Assert.True(MacroRuns.Prepare(DesktopTryout.MoveDocument(10, 20)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.ClickDocument(10, 20, 2)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.HoldDocument("F13", 500)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.RepeatClickDocument(10, 20, 1, 3, 200)).IsReady);
    }

    [Fact]
    public void An_unknown_key_is_refused_before_a_document_is_built()
    {
        Assert.Throws<ArgumentException>(() => DesktopTryout.HoldDocument("NotAKey", 10));
    }
}
