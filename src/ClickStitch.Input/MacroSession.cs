using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// Runs one macro on the execution worker.
/// Stop and emergency stop both end the run and release owned inputs.
/// A supplied wait must return when its cancellation token is cancelled, or stop cannot finish.
/// </summary>
internal static class MacroSession
{
    internal static void Start(
        ExecutionWorker worker,
        MacroRunContract contract,
        Func<VirtualScreen> screen,
        Action<MacroRunResult> completed,
        MacroPause? pause = null,
        Func<int, CancellationToken, MacroWaitSlice>? wait = null)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(completed);
        worker.Start(
        [
            WorkerActions.Run(context =>
            {
                bool ownedPause = pause is null;
                MacroPause gate = pause ?? new MacroPause();
                try
                {
                    void ReportProgress(MacroProgressEvent progress) =>
                        context.Report(ToKind(progress.Kind), actionIndex: -1, progress.Message, progress.ActionId);

                    var executor = new MacroInputExecutor(
                        context.Inputs,
                        wait ?? MacroInputExecutor.WaitWith(context.Deadline),
                        screen,
                        gate,
                        context.Cancellation,
                        ReportProgress);
                    completed(MacroRunner.Run(contract, executor, context.Cancellation, gate, ReportProgress));
                }
                finally
                {
                    if (ownedPause)
                    {
                        gate.Dispose();
                    }
                }
            }),
        ]);
    }

    /// <summary>
    /// Validates <paramref name="json"/> and starts only when it is a format-1 document.
    /// Returns false without starting the worker when validation fails.
    /// </summary>
    internal static bool TryStart(
        ExecutionWorker worker,
        string json,
        Func<VirtualScreen> screen,
        Action<MacroRunResult> completed,
        out PreparedMacro prepared,
        MacroPause? pause = null,
        Func<int, CancellationToken, MacroWaitSlice>? wait = null)
    {
        ArgumentNullException.ThrowIfNull(worker);
        prepared = MacroRuns.Prepare(json);
        if (!prepared.IsReady)
        {
            return false;
        }

        Start(worker, prepared.Contract!, screen, completed, pause, wait);
        return true;
    }

    internal static MacroDiagnostic Diagnose(ExecutionWorker worker, MacroRunResult result)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(result);
        return new MacroDiagnostic(
            result,
            worker.State,
            worker.Inputs.Snapshot(),
            worker.DrainProgress(),
            worker.ProgressDrops);
    }

    private static WorkerEventKind ToKind(MacroProgressKind kind) => kind switch
    {
        MacroProgressKind.ActionStarted => WorkerEventKind.MacroActionStarted,
        MacroProgressKind.ActionCompleted => WorkerEventKind.MacroActionCompleted,
        MacroProgressKind.Paused => WorkerEventKind.MacroPaused,
        MacroProgressKind.Resumed => WorkerEventKind.MacroResumed,
        MacroProgressKind.ActionFailed => WorkerEventKind.MacroFailed,
        MacroProgressKind.Finished => WorkerEventKind.MacroFinished,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The progress kind is not reported."),
    };
}

/// <summary>
/// A snapshot of one macro run. Reading it drains the worker progress queue.
/// There is no log file. This record is the diagnostic.
/// </summary>
internal sealed class MacroDiagnostic
{
    internal MacroDiagnostic(
        MacroRunResult result,
        WorkerState state,
        OwnedInput[] stillOwned,
        WorkerEvent[] progress,
        long progressDrops)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(stillOwned);
        ArgumentNullException.ThrowIfNull(progress);
        Result = result;
        State = state;
        StillOwned = stillOwned;
        Progress = progress;
        ProgressDrops = progressDrops;
    }

    internal MacroRunResult Result { get; }

    internal WorkerState State { get; }

    internal OwnedInput[] StillOwned { get; }

    internal WorkerEvent[] Progress { get; }

    internal long ProgressDrops { get; }

    internal bool Released => StillOwned.Length == 0;
}
