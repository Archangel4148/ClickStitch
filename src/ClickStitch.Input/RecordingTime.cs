namespace ClickStitch.Input;

/// <summary>
/// Elapsed time between two monotonic timestamps. Recording never reads the wall clock.
/// </summary>
internal static class RecordingTime
{
    internal static int ElapsedMilliseconds(long startTimestamp, long endTimestamp)
    {
        long ticks = endTimestamp - startTimestamp;
        if (ticks <= 0)
        {
            return 0;
        }

        long milliseconds = ticks * 1000 / MonotonicClock.Frequency;
        return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
    }
}
