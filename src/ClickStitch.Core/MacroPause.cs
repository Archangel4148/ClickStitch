namespace ClickStitch.Core;

/// <summary>
/// Pause for one macro run. Another thread may call <see cref="Pause"/> and <see cref="Resume"/>.
/// Pause does not release held buttons or keys, and resume does not press them again.
/// </summary>
public sealed class MacroPause : IDisposable
{
    private readonly Lock _gate = new();
    private readonly ManualResetEventSlim _running = new(initialState: true);
    private CancellationTokenSource? _wait;
    private int _paused;
    private int _blocked;
    private bool _disposed;

    public bool IsPaused => Volatile.Read(ref _paused) != 0;

    /// <summary>True while a run is blocked inside <see cref="WaitUntilRunning"/>.</summary>
    public bool IsBlocked => Volatile.Read(ref _blocked) != 0;

    public void Pause()
    {
        CancellationTokenSource? wait;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _paused = 1;
            _running.Reset();
            wait = _wait;
        }

        Cancel(wait);
    }

    public void Resume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_paused == 0)
            {
                return;
            }

            _paused = 0;
            _running.Set();
        }
    }

    /// <summary>
    /// Blocks while this pause is on. Returns false when <paramref name="cancellation"/> is cancelled.
    /// </summary>
    public bool WaitUntilRunning(CancellationToken cancellation = default)
    {
        while (Volatile.Read(ref _paused) != 0)
        {
            Interlocked.Increment(ref _blocked);
            try
            {
                _running.Wait(cancellation);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref _blocked);
            }
        }

        return !cancellation.IsCancellationRequested;
    }

    /// <summary>
    /// While attached, <see cref="Pause"/> cancels <paramref name="wait"/> so the caller can keep the remaining time.
    /// </summary>
    public void AttachWait(CancellationTokenSource wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        bool paused;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _wait = wait;
            paused = _paused != 0;
        }

        if (paused)
        {
            Cancel(wait);
        }
    }

    public void DetachWait(CancellationTokenSource wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        lock (_gate)
        {
            if (ReferenceEquals(_wait, wait))
            {
                _wait = null;
            }
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? wait;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _paused = 0;
            _running.Set();
            wait = _wait;
        }

        Cancel(wait);
        _running.Dispose();
    }

    private static void Cancel(CancellationTokenSource? wait)
    {
        if (wait is null)
        {
            return;
        }

        try
        {
            wait.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
