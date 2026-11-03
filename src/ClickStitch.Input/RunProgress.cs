namespace ClickStitch.Input;

/// <summary>
/// Prints benchmark progress from outside the measured loop.
/// A timer on another thread writes a heartbeat so a long series does not look stalled.
/// The sample loop itself does not log.
/// </summary>
internal sealed class RunProgress : IDisposable
{
    private readonly Action<string> _write;
    private readonly object _gate = new();
    private Timer? _heartbeat;
    private int _index;
    private int _total;
    private string _label = string.Empty;
    private long _started;

    internal RunProgress(Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        _write = write;
    }

    internal void Announce(string message) => _write(message);

    internal void Start(int index, int total, string label)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(total, index);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        StopHeartbeat();
        lock (_gate)
        {
            _index = index;
            _total = total;
            _label = label;
            _started = MonotonicClock.Timestamp;
            _heartbeat = new Timer(Beat, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        _write($"[{index}/{total}] {label}");
    }

    internal void Finish()
    {
        StopHeartbeat();
        int index;
        int total;
        long started;
        lock (_gate)
        {
            index = _index;
            total = _total;
            started = _started;
        }

        double seconds = MonotonicClock.Milliseconds(MonotonicClock.Timestamp - started) / 1000d;
        _write($"[{index}/{total}] finished in {seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s");
    }

    public void Dispose() => StopHeartbeat();

    private void Beat(object? state)
    {
        string line;
        lock (_gate)
        {
            if (_heartbeat is null)
            {
                return;
            }

            line = $"[{_index}/{_total}] {_label}: still running";
        }

        _write(line);
    }

    private void StopHeartbeat()
    {
        Timer? heartbeat;
        lock (_gate)
        {
            heartbeat = _heartbeat;
            _heartbeat = null;
        }

        if (heartbeat is null)
        {
            return;
        }

        using var done = new ManualResetEvent(false);
        heartbeat.Dispose(done);
        done.WaitOne();
    }
}
