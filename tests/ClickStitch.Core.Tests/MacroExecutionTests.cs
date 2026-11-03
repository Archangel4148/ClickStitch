namespace ClickStitch.Core.Tests;

public sealed class MacroExecutionTests
{
    [Fact]
    public void The_catalog_lists_the_eight_format_1_actions()
    {
        string[] types = MacroActionCatalog.BuiltIn.Select(static item => item.TypeId).ToArray();

        Assert.Equal(
            ["mouse.click", "mouse.move", "mouse.hold", "mouse.wheel", "keyboard.key", "wait", "repeat.count", "comment"],
            types);
        Assert.All(MacroActionCatalog.BuiltIn, static item => Assert.False(string.IsNullOrWhiteSpace(item.DisplayName)));
        Assert.Contains(MacroActionCatalog.CommonParameters, static item => item.Name == "comment" && !item.Required);
        Assert.True(MacroActionCatalog.TryGet("mouse.click", out MacroActionDescriptor click));
        Assert.Contains(click.Parameters, static item => item.Name == "button" && item.Required);
        Assert.False(MacroActionCatalog.TryGet("repeat.until", out _));
        Assert.False(MacroActionCatalog.TryGet("branch.if", out _));
    }

    [Fact]
    public void A_click_is_one_submission_and_a_cursor_click_has_no_point()
    {
        MacroRunContract run = Compile("""
            {
              "formatVersion": 1,
              "id": "clicks",
              "name": "Clicks",
              "actions": [
                { "id": "at", "type": "mouse.click", "button": "left", "x": 500, "y": 300, "clickCount": 2, "comment": "author note" },
                { "id": "here", "type": "mouse.click", "button": "right", "coordinateMode": "cursor" }
              ]
            }
            """);

        ClickContract at = Assert.IsType<ClickContract>(run.Actions[0]);
        Assert.Equal(MacroStopBehavior.AroundSubmission, at.Stop);
        Assert.Equal(MacroPauseBehavior.BeforeNextAction, at.Pause);
        Assert.Equal(MacroMouseButton.Left, at.Button);
        Assert.Equal(2, at.ClickCount);
        Assert.Equal(new MacroPoint(MacroCoordinateMode.VirtualDesktop, 500, 300), at.Point);

        ClickContract here = Assert.IsType<ClickContract>(run.Actions[1]);
        Assert.Null(here.Point);
        Assert.Equal(1, here.ClickCount);
    }

    [Fact]
    public void A_hold_with_a_point_keeps_the_move_in_that_same_action()
    {
        MacroRunContract run = Compile("""
            {
              "formatVersion": 1,
              "id": "holds",
              "name": "Holds",
              "actions": [
                { "id": "down", "type": "mouse.hold", "button": "left", "state": "down", "x": 4, "y": 5 },
                { "id": "up", "type": "mouse.hold", "button": "left", "state": "up" }
              ]
            }
            """);

        HoldContract down = Assert.IsType<HoldContract>(run.Actions[0]);
        Assert.Equal(new MacroPoint(MacroCoordinateMode.VirtualDesktop, 4, 5), down.Point);
        Assert.Equal(MacroStopBehavior.AroundSubmission, down.Stop);
        HoldContract up = Assert.IsType<HoldContract>(run.Actions[1]);
        Assert.Null(up.Point);
        Assert.Equal(MacroPressState.Up, up.State);
    }

    [Fact]
    public void Wait_pause_and_stop_differ_from_a_key_and_a_wheel()
    {
        MacroRunContract run = Compile("""
            {
              "formatVersion": 1,
              "id": "timing",
              "name": "Timing",
              "settings": { "stopOnError": false },
              "actions": [
                { "id": "pause", "type": "wait", "durationMs": 0 },
                { "id": "key", "type": "keyboard.key", "key": "Shift", "state": "down" },
                { "id": "wheel", "type": "mouse.wheel", "notches": 1 }
              ]
            }
            """);

        Assert.False(run.StopOnError);
        WaitContract wait = Assert.IsType<WaitContract>(run.Actions[0]);
        Assert.Equal(0, wait.DurationMs);
        Assert.Equal(MacroStopBehavior.EndWait, wait.Stop);
        Assert.Equal(MacroPauseBehavior.KeepRemainingWait, wait.Pause);
        KeyContract key = Assert.IsType<KeyContract>(run.Actions[1]);
        Assert.Equal("Shift", key.Key);
        Assert.Equal(MacroPressState.Down, key.State);
        Assert.Equal(120, WheelContract.WindowsWheelDelta);
        Assert.Equal(1, Assert.IsType<WheelContract>(run.Actions[2]).Notches);
    }

    [Fact]
    public void Repeat_keeps_its_count_and_does_not_copy_the_children_once_per_iteration()
    {
        MacroRunContract run = Compile("""
            {
              "formatVersion": 1,
              "id": "loop",
              "name": "Loop",
              "actions": [
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 1000000,
                  "actions": [
                    { "id": "note", "type": "comment", "text": "skip" },
                    { "id": "move", "type": "mouse.move", "coordinateMode": "relative", "x": 1, "y": 0 }
                  ]
                }
              ]
            }
            """);

        RepeatContract repeat = Assert.IsType<RepeatContract>(run.Actions[0]);
        Assert.Equal(1_000_000, repeat.Count);
        Assert.Equal(2, repeat.Actions.Count);
        Assert.Equal(MacroStopBehavior.BetweenRepeatSteps, repeat.Stop);
        CommentContract comment = Assert.IsType<CommentContract>(repeat.Actions[0]);
        Assert.Equal(MacroStopBehavior.Skip, comment.Stop);
        Assert.Equal("skip", comment.Text);
        MoveContract move = Assert.IsType<MoveContract>(repeat.Actions[1]);
        Assert.Equal(new MacroPoint(MacroCoordinateMode.Relative, 1, 0), move.Point);
    }

    [Fact]
    public void An_unknown_member_is_not_part_of_the_contract()
    {
        MacroRunContract run = Compile("""
            {
              "formatVersion": 1,
              "id": "plain",
              "name": "Plain",
              "editor": "list",
              "variables": [ { "name": "Count", "type": "number", "default": 1 } ],
              "actions": [ { "id": "only", "type": "mouse.click", "button": "middle", "x": 1, "y": 2, "note": true } ]
            }
            """);

        Assert.True(run.StopOnError);
        ClickContract click = Assert.IsType<ClickContract>(Assert.Single(run.Actions));
        Assert.Equal(MacroMouseButton.Middle, click.Button);
        Assert.Equal(new MacroPoint(MacroCoordinateMode.VirtualDesktop, 1, 2), click.Point);
    }

    private static MacroRunContract Compile(string json)
    {
        MacroReadResult result = MacroJson.Read(json);
        Assert.True(result.IsValid);
        return MacroExecution.Compile(result.Document!);
    }
}
