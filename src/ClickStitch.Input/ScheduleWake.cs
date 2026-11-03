namespace ClickStitch.Input;

/// <summary>
/// The moment a wait ended. On success, <see cref="Timestamp"/> is at or after <see cref="Deadline"/>.
/// Cancellation may return before the deadline.
/// </summary>
internal readonly struct ScheduleWake
{
    internal ScheduleWake(long timestamp, long deadline, bool cancelled)
    {
        Timestamp = timestamp;
        Deadline = deadline;
        Cancelled = cancelled;
    }

    internal long Timestamp { get; }

    internal long Deadline { get; }

    internal bool Cancelled { get; }

    /// <summary>
    /// Submit time minus the requested time, in clock ticks. Negative only when the wait was cancelled.
    /// </summary>
    internal long SchedulingErrorTicks => Timestamp - Deadline;
}
