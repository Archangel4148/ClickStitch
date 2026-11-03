namespace ClickStitch.Core;

public enum RecordingState
{
    Idle,
    Recording,
    Stopped,
    Cancelled,
}

public enum RecordingStop
{
    None,
    Requested,
    Limit,
}

/// <summary>
/// The life of one recording. It does not install a hook and does not build a macro.
/// Captured events are counted here. The list itself arrives with capture.
/// </summary>
public sealed class RecordingSession
{
    public const int MaximumEvents = 100_000;

    public RecordingState State { get; private set; } = RecordingState.Idle;

    public RecordingStop StopReason { get; private set; } = RecordingStop.None;

    public int Count { get; private set; }

    public string? Notice => StopReason == RecordingStop.Limit
        ? "Recording stopped after 100,000 events."
        : null;

    public void Start()
    {
        if (State == RecordingState.Recording)
        {
            throw new InvalidOperationException("A recording is already in progress.");
        }

        State = RecordingState.Recording;
        StopReason = RecordingStop.None;
        Count = 0;
    }

    public void Stop()
    {
        if (State != RecordingState.Recording)
        {
            throw new InvalidOperationException("There is no recording to stop.");
        }

        State = RecordingState.Stopped;
        StopReason = RecordingStop.Requested;
    }

    public void Cancel()
    {
        if (State != RecordingState.Recording)
        {
            throw new InvalidOperationException("There is no recording to cancel.");
        }

        Count = 0;
        State = RecordingState.Cancelled;
        StopReason = RecordingStop.None;
    }

    /// <summary>
    /// Accepts one event while recording. The event that fills the limit is kept, and the session then stops.
    /// Returns false when the session is not recording.
    /// </summary>
    public bool Add()
    {
        if (State != RecordingState.Recording)
        {
            return false;
        }

        Count++;
        if (Count == MaximumEvents)
        {
            State = RecordingState.Stopped;
            StopReason = RecordingStop.Limit;
        }

        return true;
    }
}
