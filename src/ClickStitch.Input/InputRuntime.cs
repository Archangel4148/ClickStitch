namespace ClickStitch.Input;

/// <summary>
/// Shuts down the input engine that exists in this milestone.
/// Order: refuse new work, cancel the run, release owned inputs, unregister hotkeys, and dispose the wait timer.
/// Plugins are not loaded. Nothing is written to a log file. The process is not exited; the caller reads the report.
/// </summary>
internal sealed class InputRuntime : IDisposable
{
    private readonly object _gate = new();
    private ShutdownReport? _report;

    internal InputRuntime(ExecutionWorker worker, HotkeyListener? hotkeys = null)
    {
        ArgumentNullException.ThrowIfNull(worker);
        Worker = worker;
        Hotkeys = hotkeys;
    }

    internal ExecutionWorker Worker { get; }

    internal HotkeyListener? Hotkeys { get; }

    internal ShutdownReport Shutdown()
    {
        lock (_gate)
        {
            if (_report is not null)
            {
                return _report;
            }
        }

        Worker.CloseIntake();
        Hotkeys?.CloseIntake();
        Worker.Stop();
        Hotkeys?.Stop();
        Worker.Dispose();
        Hotkeys?.Dispose();

        var report = new ShutdownReport(
            Worker.State,
            Worker.Inputs.Snapshot(),
            Worker.DrainProgress(),
            Worker.ProgressDrops,
            Hotkeys?.Fault,
            Hotkeys?.StillRegistered ?? []);

        lock (_gate)
        {
            _report ??= report;
            return _report;
        }
    }

    public void Dispose() => Shutdown();
}
