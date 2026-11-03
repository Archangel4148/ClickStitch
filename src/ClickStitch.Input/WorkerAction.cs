namespace ClickStitch.Input;

/// <summary>
/// One step the execution worker runs on its own thread.
/// </summary>
internal abstract class WorkerAction
{
    internal abstract void Execute(WorkerContext context);
}

/// <summary>
/// The worker thread's view of input, waiting, and cancellation.
/// A wait does not hold the input-ownership lock.
/// </summary>
internal sealed class WorkerContext
{
    private readonly ExecutionWorker _worker;

    internal WorkerContext(ExecutionWorker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        _worker = worker;
    }

    internal InputStateManager Inputs => _worker.Inputs;

    internal CancellationToken Cancellation => _worker.RunCancellation;

    internal bool Wait(int milliseconds) => _worker.Wait(milliseconds);

    internal void Report(string message) => _worker.Report(WorkerEventKind.Note, actionIndex: -1, message);
}

internal static class WorkerActions
{
    internal static WorkerAction Run(Action<WorkerContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new CallbackWorkerAction(action);
    }

    internal static WorkerAction Wait(int milliseconds) => new WaitWorkerAction(milliseconds);

    private sealed class CallbackWorkerAction : WorkerAction
    {
        private readonly Action<WorkerContext> _action;

        internal CallbackWorkerAction(Action<WorkerContext> action) => _action = action;

        internal override void Execute(WorkerContext context) => _action(context);
    }

    private sealed class WaitWorkerAction : WorkerAction
    {
        private readonly int _milliseconds;

        internal WaitWorkerAction(int milliseconds) => _milliseconds = milliseconds;

        internal override void Execute(WorkerContext context) => context.Wait(_milliseconds);
    }
}
