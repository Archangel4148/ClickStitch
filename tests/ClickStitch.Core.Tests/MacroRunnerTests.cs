namespace ClickStitch.Core.Tests;

public sealed class MacroRunnerTests
{
    [Fact]
    public void Actions_run_in_order_and_a_comment_is_not_dispatched()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "order",
              "name": "Order",
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "left", "x": 1, "y": 2 },
                { "id": "note", "type": "comment", "text": "skip" },
                { "id": "pause", "type": "wait", "durationMs": 5 },
                { "id": "key", "type": "keyboard.key", "key": "A", "state": "down" }
              ]
            }
            """);
        var executor = new RecordingExecutor();

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(["click", "note", "pause", "key"], result.CompletedActionIds);
        Assert.Equal(["click", "pause", "key"], executor.Calls);
    }

    [Fact]
    public void Stop_on_error_does_not_start_the_next_action()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "halt",
              "name": "Halt",
              "actions": [
                { "id": "move", "type": "mouse.move", "x": 3, "y": 4 },
                { "id": "wheel", "type": "mouse.wheel", "notches": 1 },
                { "id": "hold", "type": "mouse.hold", "button": "left", "state": "up" }
              ]
            }
            """);
        var executor = new RecordingExecutor { FailOn = "wheel" };

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Failed, result.Status);
        Assert.Equal(["move"], result.CompletedActionIds);
        Assert.Equal(["move", "wheel"], executor.Calls);
        MacroActionFailure failure = Assert.Single(result.Failures);
        Assert.Equal("wheel", failure.ActionId);
        Assert.Equal("wheel failed", failure.Message);
    }

    [Fact]
    public void A_failure_does_not_stop_later_actions_when_stop_on_error_is_false()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "continue",
              "name": "Continue",
              "settings": { "stopOnError": false },
              "actions": [
                { "id": "wheel", "type": "mouse.wheel", "notches": -1 },
                { "id": "key", "type": "keyboard.key", "key": "B", "state": "up" }
              ]
            }
            """);
        var executor = new RecordingExecutor { FailOn = "wheel" };

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Failed, result.Status);
        Assert.Equal(["key"], result.CompletedActionIds);
        Assert.Equal(["wheel", "key"], executor.Calls);
    }

    [Fact]
    public void Cancellation_before_an_action_does_not_start_it()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "cancel",
              "name": "Cancel",
              "actions": [
                { "id": "first", "type": "wait", "durationMs": 0 },
                { "id": "second", "type": "wait", "durationMs": 0 }
              ]
            }
            """);
        using var cancellation = new CancellationTokenSource();
        var executor = new RecordingExecutor { After = (_, _) => cancellation.Cancel() };

        MacroRunResult result = MacroRunner.Run(contract, executor, cancellation.Token);

        Assert.Equal(MacroRunStatus.Stopped, result.Status);
        Assert.Equal(["first"], result.CompletedActionIds);
        Assert.Empty(result.Failures);
        Assert.Equal(["first"], executor.Calls);
    }

    [Fact]
    public void A_repeat_runs_its_child_list_once_per_count()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "loop",
              "name": "Loop",
              "actions": [
                { "id": "before", "type": "comment", "text": "ok" },
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 3,
                  "actions": [ { "id": "inner", "type": "keyboard.key", "key": "C", "state": "down" } ]
                },
                { "id": "after", "type": "wait", "durationMs": 1 }
              ]
            }
            """);
        RepeatContract repeat = Assert.IsType<RepeatContract>(contract.Actions[1]);
        var executor = new RecordingExecutor();

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(["before", "inner", "inner", "inner", "again", "after"], result.CompletedActionIds);
        Assert.Equal(["inner", "inner", "inner", "after"], executor.Calls);
        Assert.Equal(3, repeat.Count);
        Assert.Single(repeat.Actions);
    }

    [Fact]
    public void A_nested_repeat_uses_the_same_child_list()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "nested",
              "name": "Nested",
              "actions": [
                {
                  "id": "outer",
                  "type": "repeat.count",
                  "count": 2,
                  "actions": [
                    {
                      "id": "inner",
                      "type": "repeat.count",
                      "count": 3,
                      "actions": [ { "id": "key", "type": "keyboard.key", "key": "D", "state": "up" } ]
                    }
                  ]
                }
              ]
            }
            """);
        var executor = new RecordingExecutor();

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(6, executor.Calls.Count);
        Assert.Equal(
            ["key", "key", "key", "inner", "key", "key", "key", "inner", "outer"],
            result.CompletedActionIds);
    }

    [Fact]
    public void A_large_count_does_not_copy_the_child_list()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "large",
              "name": "Large",
              "actions": [
                { "id": "again", "type": "repeat.count", "count": 1000000, "actions": [] },
                { "id": "after", "type": "comment", "text": "done" }
              ]
            }
            """);
        RepeatContract repeat = Assert.IsType<RepeatContract>(contract.Actions[0]);

        MacroRunResult result = MacroRunner.Run(contract, new RecordingExecutor());

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(["again", "after"], result.CompletedActionIds);
        Assert.Empty(repeat.Actions);
        Assert.Equal(1_000_000, repeat.Count);
    }

    [Fact]
    public void Cancellation_inside_a_repeat_skips_the_rest_of_the_count()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "cancel-loop",
              "name": "Cancel loop",
              "actions": [
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 5,
                  "actions": [ { "id": "step", "type": "wait", "durationMs": 0 } ]
                },
                { "id": "after", "type": "wait", "durationMs": 0 }
              ]
            }
            """);
        using var cancellation = new CancellationTokenSource();
        var executor = new RecordingExecutor { After = (_, _) => cancellation.Cancel() };

        MacroRunResult result = MacroRunner.Run(contract, executor, cancellation.Token);

        Assert.Equal(MacroRunStatus.Stopped, result.Status);
        Assert.Equal(["step"], result.CompletedActionIds);
        Assert.Equal(["step"], executor.Calls);
    }

    [Fact]
    public void A_failure_inside_a_repeat_stops_later_iterations_when_stop_on_error_is_true()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "fail-loop",
              "name": "Fail loop",
              "actions": [
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 4,
                  "actions": [ { "id": "wheel", "type": "mouse.wheel", "notches": 1 } ]
                },
                { "id": "after", "type": "comment", "text": "later" }
              ]
            }
            """);
        var executor = new RecordingExecutor { FailOn = "wheel" };

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Failed, result.Status);
        Assert.Empty(result.CompletedActionIds);
        Assert.Equal(["wheel"], executor.Calls);
        Assert.Equal("wheel", Assert.Single(result.Failures).ActionId);
    }

    [Fact]
    public void Pause_holds_the_next_action_until_resume()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "pause-next",
              "name": "Pause next",
              "actions": [
                { "id": "first", "type": "wait", "durationMs": 0 },
                { "id": "second", "type": "wait", "durationMs": 0 }
              ]
            }
            """);
        using var pause = new MacroPause();
        var executor = new RecordingExecutor
        {
            After = (id, _) =>
            {
                if (id == "first")
                {
                    pause.Pause();
                }
            },
        };

        MacroRunResult? result = null;
        var thread = new Thread(() => result = MacroRunner.Run(contract, executor, pause: pause)) { IsBackground = true };
        thread.Start();
        Assert.True(SpinWait.SpinUntil(() => pause.IsBlocked, TimeSpan.FromSeconds(2)));
        Assert.Equal(["first"], executor.Calls);
        pause.Resume();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));

        Assert.Equal(MacroRunStatus.Completed, result!.Status);
        Assert.Equal(["first", "second"], executor.Calls);
    }

    [Fact]
    public void Cancellation_while_paused_does_not_start_the_next_action()
    {
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "pause-stop",
              "name": "Pause stop",
              "actions": [
                { "id": "first", "type": "wait", "durationMs": 0 },
                { "id": "second", "type": "wait", "durationMs": 0 }
              ]
            }
            """);
        using var pause = new MacroPause();
        using var cancellation = new CancellationTokenSource();
        var executor = new RecordingExecutor { After = (_, _) => pause.Pause() };
        MacroRunResult? result = null;
        var thread = new Thread(() => result = MacroRunner.Run(contract, executor, cancellation.Token, pause)) { IsBackground = true };
        thread.Start();
        Assert.True(SpinWait.SpinUntil(() => pause.IsBlocked, TimeSpan.FromSeconds(2)));
        cancellation.Cancel();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));

        Assert.Equal(MacroRunStatus.Stopped, result!.Status);
        Assert.Equal(["first"], executor.Calls);
        Assert.DoesNotContain("second", result.CompletedActionIds);
    }

    private static MacroRunContract Compile(string json)
    {
        MacroReadResult result = MacroJson.Read(json);
        Assert.True(result.IsValid);
        return MacroExecution.Compile(result.Document!);
    }

    private sealed class RecordingExecutor : IMacroExecutor
    {
        internal List<string> Calls { get; } = [];

        internal string? FailOn { get; init; }

        internal Action<string, RecordingExecutor>? After { get; init; }

        public void Click(ClickContract action) => Done(action.ActionId);

        public void Move(MoveContract action) => Done(action.ActionId);

        public void Hold(HoldContract action) => Done(action.ActionId);

        public void Wheel(WheelContract action) => Done(action.ActionId);

        public void Key(KeyContract action) => Done(action.ActionId);

        public void Wait(WaitContract action) => Done(action.ActionId);

        private void Done(string actionId)
        {
            Calls.Add(actionId);
            if (actionId == FailOn)
            {
                throw new InvalidOperationException($"{actionId} failed");
            }

            After?.Invoke(actionId, this);
        }
    }
}
