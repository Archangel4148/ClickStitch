namespace ClickStitch.Input;

/// <summary>
/// What shutdown observed. A failed release stays in <see cref="StillOwned"/> and the worker state stays failed.
/// There is no log sink in this build, so the progress queue is the diagnostic record.
/// </summary>
internal sealed class ShutdownReport
{
    internal ShutdownReport(
        WorkerState workerState,
        OwnedInput[] stillOwned,
        WorkerEvent[] progress,
        long progressDrops,
        Exception? hotkeyFault,
        IReadOnlyList<HotkeyRole> hotkeysStillRegistered)
    {
        ArgumentNullException.ThrowIfNull(stillOwned);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(hotkeysStillRegistered);
        WorkerState = workerState;
        StillOwned = stillOwned;
        Progress = progress;
        ProgressDrops = progressDrops;
        HotkeyFault = hotkeyFault;
        HotkeysStillRegistered = hotkeysStillRegistered;
    }

    internal WorkerState WorkerState { get; }

    internal OwnedInput[] StillOwned { get; }

    internal WorkerEvent[] Progress { get; }

    internal long ProgressDrops { get; }

    internal Exception? HotkeyFault { get; }

    internal IReadOnlyList<HotkeyRole> HotkeysStillRegistered { get; }

    internal bool InputsReleased => StillOwned.Length == 0;
}
