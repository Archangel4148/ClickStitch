using System.Diagnostics;
using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class MacroDiagnosticTests
{
    private static readonly VirtualScreen Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void A_run_reports_each_action_and_releases_the_key()
    {
        using ExecutionWorker worker = CreateWorker(out CapturingNative native);
        MacroRunResult? result = null;
        Assert.True(MacroSession.TryStart(worker, """
            {
              "formatVersion": 1,
              "id": "play",
              "name": "Play",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "up", "type": "keyboard.key", "key": "A", "state": "up" }
              ]
            }
            """, () => Screen, value => result = value, out PreparedMacro prepared));
        WaitUntil(worker, WorkerState.Stopped);

        MacroDiagnostic diagnostic = MacroSession.Diagnose(worker, result!);
        Assert.True(prepared.IsReady);
        Assert.Equal(MacroRunStatus.Completed, diagnostic.Result.Status);
        Assert.Equal(WorkerState.Stopped, diagnostic.State);
        Assert.True(diagnostic.Released);
        Assert.Equal(
            ["down", "up"],
            diagnostic.Progress.Where(static item => item.Kind == WorkerEventKind.MacroActionStarted).Select(static item => item.ActionId));
        Assert.Contains(
            diagnostic.Progress,
            static item => item.Kind == WorkerEventKind.MacroFinished && item.ActionId == "play" && item.Message == "Completed");
        Assert.Equal(2, native.Calls);
    }

    [Fact]
    public void An_invalid_document_does_not_start()
    {
        using ExecutionWorker worker = CreateWorker(out CapturingNative native);
        bool started = true;

        bool ready = MacroSession.TryStart(
            worker,
            """{ "formatVersion": 2, "id": "no", "name": "No", "actions": [] }""",
            () => Screen,
            _ => started = false,
            out PreparedMacro prepared);

        Assert.False(ready);
        Assert.False(prepared.IsReady);
        Assert.NotEmpty(prepared.Issues);
        Assert.True(started);
        Assert.Equal(WorkerState.Idle, worker.State);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void A_failed_action_is_on_the_diagnostic_and_the_key_is_released()
    {
        using ExecutionWorker worker = CreateWorker(out _);
        MacroRunResult? result = null;
        MacroSession.Start(worker, MacroRuns.Prepare("""
            {
              "formatVersion": 1,
              "id": "fail",
              "name": "Fail",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "again", "type": "keyboard.key", "key": "A", "state": "down" }
              ]
            }
            """).Contract!, () => Screen, value => result = value);
        WaitUntil(worker, WorkerState.Stopped);

        MacroDiagnostic diagnostic = MacroSession.Diagnose(worker, result!);
        Assert.Equal(MacroRunStatus.Failed, diagnostic.Result.Status);
        Assert.True(diagnostic.Released);
        Assert.Contains(
            diagnostic.Progress,
            static item => item.Kind == WorkerEventKind.MacroFailed
                && item.ActionId == "again"
                && item.Message == "The input is already held.");
        Assert.Contains(diagnostic.Progress, static item => item.Kind == WorkerEventKind.MacroFinished && item.Message == "Failed");
    }

    private static ExecutionWorker CreateWorker(out CapturingNative native)
    {
        native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        return new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer));
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

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            return ((uint)inputs.Length, 0);
        }
    }
}
