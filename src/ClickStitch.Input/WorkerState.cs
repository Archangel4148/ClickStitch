namespace ClickStitch.Input;

/// <summary>
/// Lifecycle of <see cref="ExecutionWorker"/>. Stopped and Idle own no simulated inputs.
/// Failed may still own an input when Windows did not accept the release.
/// </summary>
internal enum WorkerState
{
    Idle = 0,
    Running = 1,
    Paused = 2,
    Stopping = 3,
    Stopped = 4,
    Failed = 5,
}
