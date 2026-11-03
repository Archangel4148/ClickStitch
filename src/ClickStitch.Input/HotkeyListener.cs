namespace ClickStitch.Input;

/// <summary>
/// Registers global hotkeys on a background thread and dispatches them there.
/// The thread is the one that called <c>RegisterHotKey</c>, which is what Windows requires.
/// </summary>
internal sealed class HotkeyListener : IDisposable
{
    private readonly HotkeyBook _book;
    private readonly IHotkeyTarget _target;
    private readonly IHotkeyWait _wait;
    private readonly object _stateGate = new();
    private Thread? _thread;
    private IReadOnlyList<HotkeyBindingStatus> _statuses = [];
    private Exception? _fault;
    private bool _closed;
    private bool _disposed;

    internal HotkeyListener(IHotkeyRegistry registry, IHotkeyTarget target, IHotkeyWait wait)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wait);
        _book = new HotkeyBook(registry);
        _target = target;
        _wait = wait;
    }

    internal static HotkeyListener CreateForWindows(IHotkeyTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var wait = new Win32HotkeyWait();
        return new HotkeyListener(new Win32HotkeyRegistry(), target, wait) { BindRealWait = wait.BindThread };
    }

    private Action? BindRealWait { get; init; }

    internal IReadOnlyList<HotkeyBindingStatus> Statuses => _statuses;

    internal Exception? Fault => Volatile.Read(ref _fault);

    internal IReadOnlyList<HotkeyRole> StillRegistered => [.. _book.Registered.Values];

    /// <summary>
    /// Refuses a later <see cref="Start"/>. Registrations that already succeeded are removed by <see cref="Stop"/>.
    /// </summary>
    internal void CloseIntake()
    {
        lock (_stateGate)
        {
            _closed = true;
        }
    }

    internal IReadOnlyList<HotkeyBindingStatus> Start(HotkeyBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var ready = new ManualResetEventSlim(false);
        Exception? startError = null;
        lock (_stateGate)
        {
            if (_closed)
            {
                throw new InvalidOperationException("The hotkey listener is shut down.");
            }

            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is { IsAlive: true })
            {
                throw new InvalidOperationException("The hotkey listener is already running.");
            }

            _thread = new Thread(() =>
            {
                bool pumping = false;
                try
                {
                    BindRealWait?.Invoke();
                    _statuses = _book.Register(bindings);
                    pumping = true;
                    ready.Set();
                    while (true)
                    {
                        int? id = _wait.WaitForHotkey();
                        if (id is null)
                        {
                            break;
                        }

                        try
                        {
                            _book.Dispatch(id.Value, _target);
                        }
                        catch (Exception error)
                        {
                            Remember(error);
                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    if (pumping)
                    {
                        Remember(error);
                    }
                    else
                    {
                        startError = error;
                    }

                    ready.Set();
                }
                finally
                {
                    _book.Unregister();
                }
            })
            {
                IsBackground = true,
                Name = "ClickStitch.Hotkeys",
            };
            _thread.Start();
        }

        ready.Wait();
        if (startError is not null)
        {
            _thread.Join();
            throw new InvalidOperationException("The hotkey listener failed to start.", startError);
        }

        return _statuses;
    }

    internal void Stop()
    {
        Thread? thread = _thread;
        if (thread is null)
        {
            return;
        }

        _wait.Stop();
        if (thread != Thread.CurrentThread)
        {
            thread.Join();
        }
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _closed = true;
        }

        Stop();
    }

    private void Remember(Exception error) => Interlocked.CompareExchange(ref _fault, error, null);
}
