using System.Diagnostics;
using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class MacroSessionTests
{
    private static readonly VirtualScreen Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Stop_during_a_wait_releases_the_held_key_and_skips_the_rest()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out CapturingNative native);
        MacroRunResult? result = null;
        MacroSession.Start(worker, Compile("""
            {
              "formatVersion": 1,
              "id": "stop-wait",
              "name": "Stop wait",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "pause", "type": "wait", "durationMs": 60000 },
                { "id": "up", "type": "keyboard.key", "key": "A", "state": "up" }
              ]
            }
            """), () => Screen, value => result = value);
        WaitUntilHeld(manager);

        worker.Stop();

        Assert.Equal(WorkerState.Stopped, worker.State);
        Assert.Equal(MacroRunStatus.Stopped, result!.Status);
        Assert.Equal(["down"], result.CompletedActionIds);
        Assert.Empty(manager.Snapshot());
        Assert.Equal(2, native.Calls);
    }

    [Fact]
    public void Stop_during_a_pause_releases_the_held_key()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out _);
        using var pause = new MacroPause();
        MacroRunResult? result = null;
        MacroSession.Start(
            worker,
            Compile("""
                {
                  "formatVersion": 1,
                  "id": "stop-pause",
                  "name": "Stop pause",
                  "actions": [
                    { "id": "down", "type": "keyboard.key", "key": "B", "state": "down" },
                    { "id": "pause", "type": "wait", "durationMs": 50 },
                    { "id": "up", "type": "keyboard.key", "key": "B", "state": "up" }
                  ]
                }
                """),
            () => Screen,
            value => result = value,
            pause,
            (_, _) =>
            {
                pause.Pause();
                return new MacroWaitSlice(0, true);
            });
        Assert.True(SpinWait.SpinUntil(() => pause.IsBlocked, TimeSpan.FromSeconds(2)));
        Assert.Equal([OwnedInput.Key(InputKey.B)], manager.Snapshot());

        worker.Stop();

        Assert.Equal(WorkerState.Stopped, worker.State);
        Assert.Equal(MacroRunStatus.Stopped, result!.Status);
        Assert.Equal(["down"], result.CompletedActionIds);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Emergency_stop_during_a_repeat_releases_the_held_key()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out _);
        var target = new WorkerHotkeyTarget(worker);
        MacroRunResult? result = null;
        MacroSession.Start(worker, Compile("""
            {
              "formatVersion": 1,
              "id": "stop-repeat",
              "name": "Stop repeat",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "C", "state": "down" },
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 5,
                  "actions": [ { "id": "pause", "type": "wait", "durationMs": 60000 } ]
                },
                { "id": "after", "type": "comment", "text": "no" }
              ]
            }
            """), () => Screen, value => result = value);
        WaitUntilHeld(manager);

        target.EmergencyStop();

        Assert.Equal(WorkerState.Stopped, worker.State);
        Assert.Equal(MacroRunStatus.Stopped, result!.Status);
        Assert.Equal(["down"], result.CompletedActionIds);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void A_macro_that_ends_while_holding_a_key_releases_it()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out CapturingNative native);
        MacroRunResult? result = null;
        MacroSession.Start(worker, Compile("""
            {
              "formatVersion": 1,
              "id": "held",
              "name": "Held",
              "actions": [ { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" } ]
            }
            """), () => Screen, value => result = value);
        WaitUntil(worker, WorkerState.Stopped);

        Assert.Equal(MacroRunStatus.Completed, result!.Status);
        Assert.Empty(manager.Snapshot());
        Assert.Equal(NativeInputConstants.KeyEventKeyUp, native.Packets[1].Data.Keyboard.dwFlags);
    }

    [Fact]
    public void A_failed_macro_still_releases_the_held_key()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out _);
        MacroRunResult? result = null;
        MacroSession.Start(worker, Compile("""
            {
              "formatVersion": 1,
              "id": "fail",
              "name": "Fail",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "again", "type": "keyboard.key", "key": "A", "state": "down" }
              ]
            }
            """), () => Screen, value => result = value);
        WaitUntil(worker, WorkerState.Stopped);

        Assert.Equal(MacroRunStatus.Failed, result!.Status);
        Assert.Equal("The input is already held.", Assert.Single(result.Failures).Message);
        Assert.Empty(manager.Snapshot());
    }

    private static ExecutionWorker CreateWorker(out InputStateManager manager, out CapturingNative native)
    {
        native = new CapturingNative();
        manager = new InputStateManager(new SendInputBatch(native));
        return new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer));
    }

    private static MacroRunContract Compile(string json)
    {
        MacroReadResult read = MacroJson.Read(json);
        Assert.True(read.IsValid, read.Issues.Count == 0 ? "" : read.Issues[0].Message);
        return MacroExecution.Compile(read.Document!);
    }

    private static void WaitUntil(ExecutionWorker worker, WorkerState state)
    {
        var waited = Stopwatch.StartNew();
        while (worker.State != state)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException($"Worker stayed {worker.State}.");
            }

            Thread.Sleep(5);
        }
    }

    private static void WaitUntilHeld(InputStateManager manager)
    {
        var waited = Stopwatch.StartNew();
        while (manager.Snapshot().Length == 0)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException("The worker did not press the key.");
            }

            Thread.Sleep(5);
        }
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<NativeInput> Packets { get; } = [];

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            Packets.AddRange(inputs);
            return ((uint)inputs.Length, 0);
        }
    }
}
