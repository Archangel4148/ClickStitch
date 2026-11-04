using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class ClickRecipeTests
{
    [Fact]
    public void Spam_at_the_cursor_has_no_point()
    {
        Assert.True(ClickRecipe.TryBuild("right", false, true, 0, 0, 2, 4, 50, out string json, out string problem));
        Assert.Equal("", problem);
        MacroDocument document = Read(json);
        RepeatCountAction repeat = Assert.IsType<RepeatCountAction>(Assert.Single(document.Actions));
        Assert.Equal(4, repeat.Count);
        MouseClickAction click = Assert.IsType<MouseClickAction>(repeat.Actions[0]);
        Assert.Equal(MacroMouseButton.Right, click.Button);
        Assert.Equal(MacroCoordinateMode.Cursor, click.Mode);
        Assert.Null(click.X);
        Assert.Equal(2, click.ClickCount);
        Assert.IsType<WaitAction>(repeat.Actions[1]);
    }

    [Fact]
    public void Spam_at_a_point_keeps_that_point()
    {
        Assert.True(ClickRecipe.TryBuild("left", false, false, 12, 34, 1, ClickRecipe.UntilStopped, 0, out string json, out _));
        RepeatCountAction repeat = Assert.IsType<RepeatCountAction>(Assert.Single(Read(json).Actions));
        MouseClickAction click = Assert.IsType<MouseClickAction>(repeat.Actions[0]);
        Assert.Equal(12, click.X);
        Assert.Equal(34, click.Y);
        Assert.Equal(ClickRecipe.UntilStopped, repeat.Count);
    }

    [Fact]
    public void Hold_presses_down_and_waits_instead_of_releasing()
    {
        Assert.True(ClickRecipe.TryBuild("middle", true, true, 0, 0, 1, 1, 0, out string json, out _));
        MacroDocument document = Read(json);
        MouseHoldAction hold = Assert.IsType<MouseHoldAction>(document.Actions[0]);
        Assert.Equal(MacroPressState.Down, hold.State);
        Assert.Equal(MacroCoordinateMode.Cursor, hold.Mode);
        WaitAction wait = Assert.IsType<WaitAction>(document.Actions[1]);
        Assert.Equal(ClickRecipe.HoldLimitMilliseconds, wait.DurationMs);
    }

    [Fact]
    public void A_key_taps_on_spam_and_stays_down_on_hold()
    {
        Assert.True(ClickRecipe.TryBuild("F13", false, true, 0, 0, 1, 3, 20, out string spam, out _));
        RepeatCountAction repeat = Assert.IsType<RepeatCountAction>(Assert.Single(Read(spam).Actions));
        Assert.Equal(3, repeat.Actions.Count);
        Assert.IsType<KeyboardKeyAction>(repeat.Actions[0]);
        Assert.IsType<KeyboardKeyAction>(repeat.Actions[1]);

        Assert.True(ClickRecipe.TryBuild("Space", true, true, 0, 0, 1, 1, 0, out string held, out _));
        Assert.IsType<KeyboardKeyAction>(Read(held).Actions[0]);
    }

    [Fact]
    public void A_missing_target_or_a_bad_count_is_refused()
    {
        Assert.False(ClickRecipe.TryBuild("side", false, true, 0, 0, 1, 1, 0, out _, out string problem));
        Assert.Contains("button", problem, StringComparison.Ordinal);
        Assert.False(ClickRecipe.TryBuild("left", false, true, 0, 0, 1, 0, 0, out _, out problem));
        Assert.Contains("Count", problem, StringComparison.Ordinal);
    }

    private static MacroDocument Read(string json)
    {
        MacroReadResult result = MacroJson.Read(json);
        Assert.True(result.IsValid);
        return result.Document!;
    }
}
