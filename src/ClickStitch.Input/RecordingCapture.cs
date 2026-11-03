using ClickStitch.Core;

namespace ClickStitch.Input;

internal interface IRecordingHook
{
    /// <summary>
    /// Installs the hook. <paramref name="onSample"/> must return quickly.
    /// Returns false when installation fails. The hook is not left installed in that case.
    /// </summary>
    bool Install(Action<HookSample> onSample);

    void Uninstall();
}

/// <summary>
/// Accepts samples while a <see cref="RecordingSession"/> is recording.
/// The hook callback only enqueues. <see cref="Collect"/> copies the queue into the session.
/// A second down of a button or key that is already down is ignored.
/// A move that repeats the last accepted point is ignored. Movement itself is off unless the options enable it.
/// <see cref="Simplify"/> copies a shorter path from the raw list. <see cref="Revert"/> discards that copy.
/// </summary>
internal sealed class RecordingCapture : IDisposable
{
    private readonly RecordingSession _session = new();
    private readonly IRecordingHook _hook;
    private readonly RecordingOptions _options;
    private readonly Lock _gate = new();
    private readonly Queue<RecordedEvent> _pending = new();
    private readonly List<RecordedEvent> _events = [];
    private List<RecordedEvent>? _simplified;
    private readonly HashSet<(bool IsKey, int Code)> _held = [];
    private bool _hasPoint;
    private int _pointX;
    private int _pointY;
    private bool _disposed;

    internal RecordingCapture(IRecordingHook hook, RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _hook = hook;
        _options = options;
    }

    internal RecordingCapture(IRecordingHook hook)
        : this(hook, RecordingOptions.Default)
    {
    }

    internal RecordingSession Session => _session;

    internal RecordingOptions Options => _options;

    /// <summary>Monotonic timestamp taken when <see cref="Start"/> began this recording.</summary>
    internal long StartedAt { get; private set; }

    /// <summary>The samples the hook accepted. Simplification does not change this list.</summary>
    internal IReadOnlyList<RecordedEvent> Events => _events;

    /// <summary>The shortened copy from <see cref="Simplify"/>, or null when the raw list is current.</summary>
    internal IReadOnlyList<RecordedEvent>? Simplified => _simplified;

    internal int ElapsedMilliseconds(RecordedEvent recorded) =>
        RecordingTime.ElapsedMilliseconds(StartedAt, recorded.Timestamp);

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MovementOptimization.From(_options).Validate();
        _session.Start();
        StartedAt = MonotonicClock.Timestamp;
        lock (_gate)
        {
            _pending.Clear();
            _events.Clear();
            _simplified = null;
            _held.Clear();
            _hasPoint = false;
        }

        bool installed;
        try
        {
            installed = _hook.Install(OnSample);
        }
        catch
        {
            _session.Cancel();
            throw;
        }

        if (!installed)
        {
            _hook.Uninstall();
            _session.Cancel();
            throw new InvalidOperationException("The recording hook could not be installed.");
        }
    }

    internal void Collect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Drain();
    }

    internal void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.State != RecordingState.Recording)
        {
            if (_session.StopReason == RecordingStop.Limit)
            {
                _hook.Uninstall();
                return;
            }

            _session.Stop();
        }

        _hook.Uninstall();
        Drain();
        if (_session.State == RecordingState.Recording)
        {
            _session.Stop();
        }
    }

    internal void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.State != RecordingState.Recording)
        {
            _session.Cancel();
        }

        _hook.Uninstall();
        lock (_gate)
        {
            _pending.Clear();
            _held.Clear();
            _hasPoint = false;
        }

        _events.Clear();
        _simplified = null;
        _session.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _hook.Uninstall();
        Drain();
        if (_session.State == RecordingState.Recording)
        {
            _session.Stop();
        }

        _disposed = true;
    }

    /// <summary>
    /// Stores a shorter copy of <see cref="Events"/> using the capture options.
    /// The raw list stays. Call this only after the recording has stopped.
    /// </summary>
    internal void Simplify() => Simplify(MovementOptimization.From(_options));

    /// <summary>
    /// Stores a shorter copy of <see cref="Events"/>. A second call replaces that copy.
    /// An invalid optimization leaves the previous copy in place.
    /// </summary>
    internal void Simplify(MovementOptimization optimization)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.State != RecordingState.Stopped)
        {
            throw new InvalidOperationException(
                _session.State == RecordingState.Recording
                    ? "Stop the recording before simplifying it."
                    : "There is no recording to simplify.");
        }

        optimization.Validate();
        _simplified = RecordingPath.Build(_events, optimization);
    }

    /// <summary>
    /// Removes moves and button presses whose point lies in the rectangle. Keys stay.
    /// The session count is unchanged. Call this after the recording has stopped and before saving.
    /// </summary>
    internal void OmitInside(int left, int top, int right, int bottom)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _events.RemoveAll(recorded =>
            recorded.Kind is RecordedKind.Move or RecordedKind.Button &&
            recorded.X >= left && recorded.X < right &&
            recorded.Y >= top && recorded.Y < bottom);
    }

    /// <summary>
    /// Builds a format-1 macro from the shortened copy when one exists, otherwise from the raw list.
    /// The lists stay in memory.
    /// </summary>
    internal RecordedDocument ToDocument(string name, string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.State != RecordingState.Stopped)
        {
            throw new InvalidOperationException(
                _session.State == RecordingState.Recording
                    ? "Stop the recording before saving it."
                    : "There is no recording to save.");
        }

        IReadOnlyList<RecordedEvent> source = _simplified ?? _events;
        return RecordingCompiler.Compile(source, name, id, _options.IncludeModifiers);
    }

    /// <summary>Drops the shortened copy. The raw list remains.</summary>
    internal void Revert()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _simplified = null;
    }

    private void Drain()
    {
        RecordedEvent[] batch;
        lock (_gate)
        {
            batch = _pending.ToArray();
            _pending.Clear();
        }

        foreach (RecordedEvent recorded in batch)
        {
            if (!_session.Add())
            {
                break;
            }

            _events.Add(recorded);
        }

        if (_session.StopReason == RecordingStop.Limit)
        {
            _hook.Uninstall();
        }
    }

    private void OnSample(HookSample sample)
    {
        if (!RecordingDecoder.TryDecode(sample, _options, out RecordedEvent recorded))
        {
            return;
        }

        lock (_gate)
        {
            if (_session.State != RecordingState.Recording || _pending.Count >= RecordingSession.MaximumEvents)
            {
                return;
            }

            if (recorded.Kind is RecordedKind.Key or RecordedKind.Button)
            {
                var token = recorded.IsKey ? (true, (int)recorded.Key) : (false, (int)recorded.Button);
                if (recorded.Down && !_held.Add(token))
                {
                    return;
                }

                if (!recorded.Down)
                {
                    _held.Remove(token);
                }
            }
            else if (recorded.Kind == RecordedKind.Move)
            {
                if (_hasPoint && _pointX == recorded.X && _pointY == recorded.Y)
                {
                    return;
                }

                _hasPoint = true;
                _pointX = recorded.X;
                _pointY = recorded.Y;
            }

            _pending.Enqueue(recorded);
        }
    }
}
