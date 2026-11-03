namespace ClickStitch.Core;

/// <summary>
/// Receives one leaf action. An implementation may submit input. The runner does not.
/// </summary>
public interface IMacroExecutor
{
    void Click(ClickContract action);

    void Move(MoveContract action);

    void Hold(HoldContract action);

    void Wheel(WheelContract action);

    void Key(KeyContract action);

    void Wait(WaitContract action);
}

public enum MacroRunStatus
{
    Completed,
    Stopped,
    Failed,
}

public sealed class MacroActionFailure
{
    internal MacroActionFailure(string actionId, string message)
    {
        ActionId = actionId;
        Message = message;
    }

    public string ActionId { get; }

    public string Message { get; }
}

/// <summary>
/// What a run did. <see cref="MacroRunStatus.Failed"/> means at least one action failed.
/// When <see cref="MacroRunContract.StopOnError"/> is false, later actions can still be completed.
/// </summary>
public sealed class MacroRunResult
{
    internal MacroRunResult(
        MacroRunStatus status,
        IReadOnlyList<string> completedActionIds,
        IReadOnlyList<MacroActionFailure> failures)
    {
        Status = status;
        CompletedActionIds = completedActionIds;
        Failures = failures;
    }

    public MacroRunStatus Status { get; }

    public IReadOnlyList<string> CompletedActionIds { get; }

    public IReadOnlyList<MacroActionFailure> Failures { get; }
}

/// <summary>
/// Runs a contract in order, on the calling thread.
/// A comment is completed and not dispatched.
/// <see cref="RepeatContract"/> runs its child list once per count and does not copy that list.
/// Cancellation is checked before each child and before each iteration.
/// <see cref="MacroPause"/> holds the next action. A click, move, hold, wheel, or key that has started still finishes.
/// </summary>
public static class MacroRunner
{
    public static MacroRunResult Run(
        MacroRunContract contract,
        IMacroExecutor executor,
        CancellationToken cancellation = default,
        MacroPause? pause = null,
        Action<MacroProgressEvent>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(executor);
        var completed = new List<string>();
        var failures = new List<MacroActionFailure>();
        StepOutcome outcome = RunList(
            contract.Actions, contract.StopOnError, executor, cancellation, pause, progress, completed, failures);
        MacroRunStatus status = outcome switch
        {
            StepOutcome.Stopped => MacroRunStatus.Stopped,
            StepOutcome.Failed => MacroRunStatus.Failed,
            _ => failures.Count == 0 ? MacroRunStatus.Completed : MacroRunStatus.Failed,
        };
        Report(progress, MacroProgressKind.Finished, contract.MacroId, status.ToString());
        return new MacroRunResult(status, completed, failures);
    }

    private static StepOutcome RunList(
        IReadOnlyList<MacroActionContract> actions,
        bool stopOnError,
        IMacroExecutor executor,
        CancellationToken cancellation,
        MacroPause? pause,
        Action<MacroProgressEvent>? progress,
        List<string> completed,
        List<MacroActionFailure> failures)
    {
        foreach (MacroActionContract action in actions)
        {
            if (!Continue(cancellation, pause, progress, action.ActionId))
            {
                return StepOutcome.Stopped;
            }

            StepOutcome outcome = RunOne(action, stopOnError, executor, cancellation, pause, progress, completed, failures);
            if (outcome != StepOutcome.Continue)
            {
                return outcome;
            }
        }

        return StepOutcome.Continue;
    }

    private static StepOutcome RunOne(
        MacroActionContract action,
        bool stopOnError,
        IMacroExecutor executor,
        CancellationToken cancellation,
        MacroPause? pause,
        Action<MacroProgressEvent>? progress,
        List<string> completed,
        List<MacroActionFailure> failures)
    {
        if (action is CommentContract)
        {
            Report(progress, MacroProgressKind.ActionStarted, action.ActionId, action.TypeId);
            completed.Add(action.ActionId);
            Report(progress, MacroProgressKind.ActionCompleted, action.ActionId, null);
            return StepOutcome.Continue;
        }

        if (action is RepeatContract repeat)
        {
            return RunRepeat(repeat, stopOnError, executor, cancellation, pause, progress, completed, failures);
        }

        Report(progress, MacroProgressKind.ActionStarted, action.ActionId, action.TypeId);
        try
        {
            Dispatch(executor, action);
            completed.Add(action.ActionId);
            Report(progress, MacroProgressKind.ActionCompleted, action.ActionId, null);
            return StepOutcome.Continue;
        }
        catch (OperationCanceledException)
        {
            return StepOutcome.Stopped;
        }
        catch (Exception error)
        {
            failures.Add(new MacroActionFailure(action.ActionId, error.Message));
            Report(progress, MacroProgressKind.ActionFailed, action.ActionId, error.Message);
            return stopOnError ? StepOutcome.Failed : StepOutcome.Continue;
        }
    }

    private static StepOutcome RunRepeat(
        RepeatContract repeat,
        bool stopOnError,
        IMacroExecutor executor,
        CancellationToken cancellation,
        MacroPause? pause,
        Action<MacroProgressEvent>? progress,
        List<string> completed,
        List<MacroActionFailure> failures)
    {
        Report(progress, MacroProgressKind.ActionStarted, repeat.ActionId, repeat.TypeId);
        for (int iteration = 0; iteration < repeat.Count; iteration++)
        {
            if (!Continue(cancellation, pause, progress, repeat.ActionId))
            {
                return StepOutcome.Stopped;
            }

            StepOutcome outcome = RunList(repeat.Actions, stopOnError, executor, cancellation, pause, progress, completed, failures);
            if (outcome != StepOutcome.Continue)
            {
                return outcome;
            }
        }

        completed.Add(repeat.ActionId);
        Report(progress, MacroProgressKind.ActionCompleted, repeat.ActionId, null);
        return StepOutcome.Continue;
    }

    private static bool Continue(
        CancellationToken cancellation,
        MacroPause? pause,
        Action<MacroProgressEvent>? progress,
        string nextActionId)
    {
        if (pause is null)
        {
            return !cancellation.IsCancellationRequested;
        }

        bool paused = pause.IsPaused;
        if (paused)
        {
            Report(progress, MacroProgressKind.Paused, nextActionId, null);
        }

        if (!pause.WaitUntilRunning(cancellation))
        {
            return false;
        }

        if (paused)
        {
            Report(progress, MacroProgressKind.Resumed, nextActionId, null);
        }

        return !cancellation.IsCancellationRequested;
    }

    private static void Report(Action<MacroProgressEvent>? progress, MacroProgressKind kind, string actionId, string? message)
    {
        progress?.Invoke(new MacroProgressEvent(kind, actionId, message));
    }

    private enum StepOutcome
    {
        Continue,
        Stopped,
        Failed,
    }

    private static void Dispatch(IMacroExecutor executor, MacroActionContract action)
    {
        switch (action)
        {
            case ClickContract click:
                executor.Click(click);
                break;
            case MoveContract move:
                executor.Move(move);
                break;
            case HoldContract hold:
                executor.Hold(hold);
                break;
            case WheelContract wheel:
                executor.Wheel(wheel);
                break;
            case KeyContract key:
                executor.Key(key);
                break;
            case WaitContract wait:
                executor.Wait(wait);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
