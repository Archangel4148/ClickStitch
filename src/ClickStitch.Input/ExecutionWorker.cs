namespace ClickStitch.Input;

/// <summary>
/// One background thread that submits input and waits. The caller does not run the actions.
/// Stop is safe to call more than once. Pause leaves held inputs down. Stop releases them.
/// Progress is a bounded queue: when it is full, the oldest event is dropped and the worker does not wait.
/// </summary>
internal sealed class ExecutionWorker : IDisposable
{
    internal const int DefaultProgressCapacity = 1000;
    private const int MaximumWaitMilliseconds = 3_600_000;

    private readonly DeadlineWait _wait;
    private readonly Lock _gate = new();
    private readonly object _progressGate = new();
    private readonly Queue<WorkerEvent> _progress = new();
    private readonly int _progressCapacity;
    private readonly ManualResetEventSlim _resume = new(initialState: true);
    private readonly WorkerContext _context;

    private WorkerAction[] _actions = [];
    private CancellationTokenSource _run = new();
    private CancellationTokenSource? _sliceCancel;
    private Thread? _thread;
    private WorkerState _state = WorkerState.Idle;
    private long _progressDrops;
    private bool _closed;
    private bool _disposed;

    internal ExecutionWorker(InputStateManager inputs, DeadlineWait wait, int progressCapacity = DefaultProgressCapacity)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentOutOfRangeException.ThrowIfLessThan(progressCapacity, 1);
        Inputs = inputs;
        _wait = wait;
        _progressCapacity = progressCapacity;
        _context = new WorkerContext(this);
        Inputs.BindWorker(this);
    }

    internal static ExecutionWorker CreateForWindows() =>
        new(InputStateManager.CreateForWindows(), DeadlineWait.CreateForWindows());

    internal InputStateManager Inputs { get; }

    internal WorkerState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    internal long ProgressDrops
    {
        get
        {
            lock (_progressGate)
            {
                return _progressDrops;
            }
        }
    }

    internal CancellationToken RunCancellation
    {
        get
        {
            lock (_gate)
            {
                return _run.Token;
            }
        }
    }

    internal void Start(IReadOnlyList<WorkerAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var copy = new WorkerAction[actions.Count];
        for (int index = 0; index < actions.Count; index++)
        {
            copy[index] = actions[index] ?? throw new ArgumentException("An action is null.", nameof(actions));
        }

        lock (_gate)
        {
            if (_closed)
            {
                throw new InvalidOperationException("The worker is shut down.");
            }

            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
            {
                throw new InvalidOperationException($"Cannot start while the worker is {_state}.");
            }

            if (Inputs.Snapshot().Length != 0)
            {
                throw new InvalidOperationException("Cannot start while an input is still held.");
            }

            _actions = copy;
            _run.Dispose();
            _run = new CancellationTokenSource();
            _resume.Set();
            _state = WorkerState.Running;
            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "ClickStitch.Execution",
            };
            _thread.Start();
        }
    }

    internal void Pause()
    {
        CancellationTokenSource? slice;
        bool changed = false;
        lock (_gate)
        {
            if (_state == WorkerState.Running)
            {
                _state = WorkerState.Paused;
                _resume.Reset();
                changed = true;
            }

            slice = _sliceCancel;
        }

        Cancel(slice);
        if (changed)
        {
            Report(WorkerEventKind.Paused, -1, null);
        }
    }

    internal void Resume()
    {
        bool changed = false;
        lock (_gate)
        {
            if (_state == WorkerState.Paused)
            {
                _state = WorkerState.Running;
                _resume.Set();
                changed = true;
            }
        }

        if (changed)
        {
            Report(WorkerEventKind.Resumed, -1, null);
        }
    }

    /// <summary>
    /// Refuses later <see cref="Start"/> calls. Stop and release are separate, so a held input is still released.
    /// </summary>
    internal void CloseIntake()
    {
        lock (_gate)
        {
            _closed = true;
        }
    }

    /// <summary>
    /// Asks the worker to leave the current wait, skip later actions, and release owned inputs.
    /// Returns after that release attempt, except when called on the worker thread itself.
    /// </summary>
    internal void Stop()
    {
        Thread? thread;
        CancellationTokenSource? run;
        CancellationTokenSource? slice;
        lock (_gate)
        {
            if (_disposed || _state is WorkerState.Idle or WorkerState.Stopped or WorkerState.Failed)
            {
                return;
            }

            _state = WorkerState.Stopping;
            _resume.Set();
            run = _run;
            slice = _sliceCancel;
            thread = _thread;
        }

        Cancel(run);
        Cancel(slice);
        if (thread is not null && thread != Thread.CurrentThread)
        {
            thread.Join();
        }
    }

    internal WorkerEvent[] DrainProgress()
    {
        lock (_progressGate)
        {
            var events = new WorkerEvent[_progress.Count];
            int index = 0;
            while (_progress.Count > 0)
            {
                events[index++] = _progress.Dequeue();
            }

            return events;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Inputs.UnbindWorker(this);
        _run.Dispose();
        _resume.Dispose();
        _wait.Dispose();
    }

    internal bool Wait(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(milliseconds, MaximumWaitMilliseconds);
        if (!Continue())
        {
            return false;
        }

        long remaining = MonotonicClock.TicksFromMilliseconds(milliseconds);
        while (remaining > 0)
        {
            if (!Continue())
            {
                return false;
            }

            using var slice = new CancellationTokenSource();
            bool armed;
            lock (_gate)
            {
                armed = _state == WorkerState.Running && !_run.IsCancellationRequested;
                if (armed)
                {
                    _sliceCancel = slice;
                }
            }

            if (!armed)
            {
                continue;
            }

            long started = MonotonicClock.Timestamp;
            try
            {
                _wait.WaitUntil(started + remaining, slice.Token);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_sliceCancel, slice))
                    {
                        _sliceCancel = null;
                    }
                }
            }

            long elapsed = MonotonicClock.Timestamp - started;
            remaining = elapsed >= remaining ? 0 : remaining - elapsed;
        }

        return Continue();
    }

    internal void Report(WorkerEventKind kind, int actionIndex, string? message)
    {
        var workerEvent = new WorkerEvent(kind, actionIndex, message, MonotonicClock.Timestamp);
        lock (_progressGate)
        {
            if (_progress.Count == _progressCapacity)
            {
                _progress.Dequeue();
                _progressDrops++;
            }

            _progress.Enqueue(workerEvent);
        }
    }

    private void RunLoop()
    {
        try
        {
            Report(WorkerEventKind.Started, -1, null);
            for (int index = 0; index < _actions.Length; index++)
            {
                if (!Continue())
                {
                    break;
                }

                Report(WorkerEventKind.ActionStarted, index, null);
                try
                {
                    _actions[index].Execute(_context);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!Continue())
                {
                    break;
                }

                Report(WorkerEventKind.ActionCompleted, index, null);
            }

            Finish(failure: null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private void Finish(Exception? failure)
    {
        Exception? releaseError = null;
        try
        {
            OwnershipResult released = Inputs.ReleaseAllOwnedInputs();
            if (released.StillOwned.Length != 0)
            {
                releaseError = new InvalidOperationException(
                    $"Release left {released.StillOwned.Length} input(s) held.");
            }
        }
        catch (Exception error)
        {
            releaseError = error;
        }

        string? message = failure?.Message;
        if (releaseError is not null)
        {
            message = message is null ? releaseError.Message : $"{message} {releaseError.Message}";
        }

        bool failed = failure is not null || releaseError is not null;
        lock (_gate)
        {
            _state = failed ? WorkerState.Failed : WorkerState.Stopped;
        }

        Report(failed ? WorkerEventKind.Failed : WorkerEventKind.Stopped, -1, message);
    }

    private bool Continue()
    {
        while (true)
        {
            lock (_gate)
            {
                if (_run.IsCancellationRequested || _state == WorkerState.Stopping)
                {
                    return false;
                }

                if (_state == WorkerState.Running)
                {
                    return true;
                }
            }

            _resume.Wait();
        }
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
