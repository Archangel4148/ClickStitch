namespace ClickStitch.Input;

internal enum WorkerEventKind
{
    Started,
    ActionStarted,
    ActionCompleted,
    Paused,
    Resumed,
    Stopped,
    Failed,
    Note,
    MacroActionStarted,
    MacroActionCompleted,
    MacroPaused,
    MacroResumed,
    MacroFailed,
    MacroFinished,
}

/// <summary>
/// One progress record. The worker never waits on the reader. A full queue drops the oldest record.
/// </summary>
internal readonly struct WorkerEvent
{
    internal WorkerEvent(WorkerEventKind kind, int actionIndex, string? message, long timestamp, string? actionId = null)
    {
        Kind = kind;
        ActionIndex = actionIndex;
        Message = message;
        Timestamp = timestamp;
        ActionId = actionId;
    }

    internal WorkerEventKind Kind { get; }

    internal int ActionIndex { get; }

    internal string? Message { get; }

    internal long Timestamp { get; }

    /// <summary>The macro or action id for a macro progress record. Null for worker records.</summary>
    internal string? ActionId { get; }
}
