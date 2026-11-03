namespace ClickStitch.Input;

/// <summary>
/// Connects the four hotkeys to one execution worker.
/// Emergency stop calls <see cref="ExecutionWorker.Stop"/> and nothing else.
/// </summary>
internal sealed class WorkerHotkeyTarget : IHotkeyTarget
{
    private readonly ExecutionWorker _worker;
    private readonly Action? _start;

    internal WorkerHotkeyTarget(ExecutionWorker worker, Action? start = null)
    {
        ArgumentNullException.ThrowIfNull(worker);
        _worker = worker;
        _start = start;
    }

    public void Start() => _start?.Invoke();

    public void PauseOrResume()
    {
        if (_worker.State == WorkerState.Paused)
        {
            _worker.Resume();
        }
        else if (_worker.State == WorkerState.Running)
        {
            _worker.Pause();
        }
    }

    public void Stop() => _worker.Stop();

    public void EmergencyStop() => _worker.Stop();
}
