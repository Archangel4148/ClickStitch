using System.Diagnostics;

namespace ClickStitch.Input;

/// <summary>
/// Monotonic timestamps from <see cref="Stopwatch.GetTimestamp"/>.
/// Scheduling never reads the wall clock. A timestamp has no meaning on its own;
/// subtract two timestamps and convert with <see cref="Frequency"/>.
/// </summary>
internal static class MonotonicClock
{
    internal static long Frequency { get; } = Stopwatch.Frequency;

    internal static bool IsHighResolution { get; } = Stopwatch.IsHighResolution;

    internal static long Timestamp => Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts milliseconds to clock ticks, rounding up.
    /// A requested wait is never shorter than the caller asked for.
    /// </summary>
    internal static long TicksFromMilliseconds(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        return Ceiling(milliseconds, 1000);
    }

    /// <summary>
    /// Converts microseconds to clock ticks, rounding up.
    /// </summary>
    internal static long TicksFromMicroseconds(int microseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(microseconds);
        return Ceiling(microseconds, 1_000_000);
    }

    /// <summary>
    /// Relative due time for <c>SetWaitableTimer</c>, in 100-nanosecond units, negative.
    /// The magnitude is rounded up so the timer is not asked to wake before the deadline.
    /// </summary>
    internal static long RelativeDueTime100Ns(long remainingTicks)
    {
        if (remainingTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingTicks), "The timer due time needs a future deadline.");
        }

        if (remainingTicks > long.MaxValue / 10_000_000L)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingTicks), "The wait is longer than this clock can express.");
        }

        long units = (remainingTicks * 10_000_000L + Frequency - 1) / Frequency;
        if (units <= 0)
        {
            units = 1;
        }

        return -units;
    }

    internal static double Milliseconds(long ticks) => ticks * 1000d / Frequency;

    private static long Ceiling(int amount, int unitsPerSecond)
    {
        if (amount > long.MaxValue / Frequency)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The duration is longer than this clock can express.");
        }

        return (amount * Frequency + unitsPerSecond - 1) / unitsPerSecond;
    }
}
