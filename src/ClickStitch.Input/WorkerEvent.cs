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
}

/// <summary>
/// One progress record. The worker never waits on the reader. A full queue drops the oldest record.
/// </summary>
internal readonly struct WorkerEvent
{
    internal WorkerEvent(WorkerEventKind kind, int actionIndex, string? message, long timestamp)
    {
        Kind = kind;
        ActionIndex = actionIndex;
        Message = message;
        Timestamp = timestamp;
    }

    internal WorkerEventKind Kind { get; }

    internal int ActionIndex { get; }

    internal string? Message { get; }

    internal long Timestamp { get; }
}
