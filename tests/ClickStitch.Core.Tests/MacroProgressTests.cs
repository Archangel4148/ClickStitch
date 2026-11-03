namespace ClickStitch.Core.Tests;

public sealed class MacroProgressTests
{
    [Fact]
    public void Progress_names_each_action_once_and_a_repeat_is_not_copied()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "play",
              "name": "Play",
              "actions": [
                { "id": "note", "type": "comment", "text": "skip" },
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 2,
                  "actions": [ { "id": "key", "type": "keyboard.key", "key": "A", "state": "up" } ]
                }
              ]
            }
            """);
        var progress = new List<MacroProgressEvent>();

        MacroRunResult result = MacroRunner.Run(contract, new RecordingExecutor(), progress: progress.Add);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(
            [
                new MacroProgressEvent(MacroProgressKind.ActionStarted, "note", "comment"),
                new MacroProgressEvent(MacroProgressKind.ActionCompleted, "note", null),
                new MacroProgressEvent(MacroProgressKind.ActionStarted, "again", "repeat.count"),
                new MacroProgressEvent(MacroProgressKind.ActionStarted, "key", "keyboard.key"),
                new MacroProgressEvent(MacroProgressKind.ActionCompleted, "key", null),
                new MacroProgressEvent(MacroProgressKind.ActionStarted, "key", "keyboard.key"),
                new MacroProgressEvent(MacroProgressKind.ActionCompleted, "key", null),
                new MacroProgressEvent(MacroProgressKind.ActionCompleted, "again", null),
                new MacroProgressEvent(MacroProgressKind.Finished, "play", "Completed"),
            ],
            progress);
        Assert.Equal(1_000_000, CountOf(Compile("""
            {
              "formatVersion": 1,
              "id": "large",
              "name": "Large",
              "actions": [ { "id": "again", "type": "repeat.count", "count": 1000000, "actions": [] } ]
            }
            """)));
    }

    [Fact]
    public void A_large_repeat_reports_the_repeat_once()
    {
        var progress = new List<MacroProgressEvent>();
        MacroRunner.Run(Compile("""
            {
              "formatVersion": 1,
              "id": "large",
              "name": "Large",
              "actions": [ { "id": "again", "type": "repeat.count", "count": 1000000, "actions": [] } ]
            }
            """), new RecordingExecutor(), progress: progress.Add);

        Assert.Equal(3, progress.Count);
        Assert.Equal(MacroProgressKind.ActionStarted, progress[0].Kind);
        Assert.Equal(MacroProgressKind.ActionCompleted, progress[1].Kind);
        Assert.Equal(MacroProgressKind.Finished, progress[2].Kind);
    }

    [Fact]
    public void Pause_and_failure_are_progress_records()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "halt",
              "name": "Halt",
              "actions": [
                { "id": "first", "type": "wait", "durationMs": 0 },
                { "id": "second", "type": "wait", "durationMs": 0 }
              ]
            }
            """);
        using var pause = new MacroPause();
        var progress = new List<MacroProgressEvent>();
        var executor = new RecordingExecutor { After = (id, _) => { if (id == "first") pause.Pause(); } };
        MacroRunResult? result = null;
        var thread = new Thread(() => result = MacroRunner.Run(contract, executor, pause: pause, progress: progress.Add))
        {
            IsBackground = true,
        };
        thread.Start();
        Assert.True(SpinWait.SpinUntil(() => pause.IsBlocked, TimeSpan.FromSeconds(2)));
        Assert.Contains(progress, static item => item.Kind == MacroProgressKind.Paused && item.ActionId == "second");
        pause.Resume();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));

        Assert.Equal(MacroRunStatus.Completed, result!.Status);
        Assert.Contains(progress, static item => item.Kind == MacroProgressKind.Resumed && item.ActionId == "second");
    }

    [Fact]
    public void An_invalid_document_is_not_ready_to_run()
    {
        PreparedMacro prepared = MacroRuns.Prepare("""
            {
              "formatVersion": 2,
              "id": "later",
              "name": "Later",
              "actions": []
            }
            """);

        Assert.False(prepared.IsReady);
        Assert.Null(prepared.Contract);
        Assert.NotEmpty(prepared.Issues);
    }

    [Fact]
    public void A_valid_document_is_ready_to_run()
    {
        PreparedMacro prepared = MacroRuns.Prepare("""
            {
              "formatVersion": 1,
              "id": "ready",
              "name": "Ready",
              "actions": [ { "id": "click", "type": "mouse.click", "button": "left", "coordinateMode": "cursor" } ]
            }
            """);

        Assert.True(prepared.IsReady);
        Assert.Empty(prepared.Issues);
        ClickContract click = Assert.IsType<ClickContract>(Assert.Single(prepared.Contract!.Actions));
        Assert.Equal("click", click.ActionId);
    }

    private static int CountOf(MacroRunContract contract)
    {
        RepeatContract repeat = Assert.IsType<RepeatContract>(Assert.Single(contract.Actions));
        Assert.Empty(repeat.Actions);
        return repeat.Count;
    }

    private static MacroRunContract Compile(string json)
    {
        PreparedMacro prepared = MacroRuns.Prepare(json);
        Assert.True(prepared.IsReady);
        return prepared.Contract!;
    }

    private sealed class RecordingExecutor : IMacroExecutor
    {
        internal Action<string, RecordingExecutor>? After { get; init; }

        public void Click(ClickContract action) => Done(action.ActionId);

        public void Move(MoveContract action) => Done(action.ActionId);

        public void Hold(HoldContract action) => Done(action.ActionId);

        public void Wheel(WheelContract action) => Done(action.ActionId);

        public void Key(KeyContract action) => Done(action.ActionId);

        public void Wait(WaitContract action) => Done(action.ActionId);

        private void Done(string actionId) => After?.Invoke(actionId, this);
    }
}
