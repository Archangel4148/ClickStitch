using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Waits until a monotonic deadline and does not return early unless cancellation was requested.
/// One instance is used by one thread. The caller must not hold the input-ownership lock across the wait.
/// This type never calls <c>timeBeginPeriod</c> and never raises the thread priority.
/// </summary>
internal sealed class DeadlineWait : IDisposable
{
    /// <summary>
    /// Last stretch of a hybrid wait, in microseconds. The timer parks the thread until this window,
    /// then a tight loop watches the clock. The window is wider than a short click interval on purpose:
    /// a 1 ms wait spins, and a 10 ms wait parks for most of the time.
    /// </summary>
    internal const int HybridSpinWindowMicroseconds = 1500;

    /// <summary>
    /// Wait used by <see cref="CreateForWindows"/>. Milestone 1.4 sets this from the benchmark.
    /// </summary>
    internal const WaitStrategy ProductStrategy = WaitStrategy.Hybrid;

    private const uint WaitInfinite = 0xFFFFFFFF;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const uint WaitTimeout = 0x00000102;

    private readonly WaitStrategy _strategy;
    private readonly long _spinWindowTicks;
    private readonly ManualResetEvent _cancelled = new(false);
    private readonly IntPtr[] _handles = new IntPtr[2];
    private HighResolutionTimer? _timer;
    private bool _disposed;

    internal DeadlineWait(WaitStrategy strategy, int hybridSpinWindowMicroseconds = HybridSpinWindowMicroseconds)
    {
        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(hybridSpinWindowMicroseconds);
        _strategy = strategy;
        _spinWindowTicks = strategy == WaitStrategy.Hybrid
            ? MonotonicClock.TicksFromMicroseconds(hybridSpinWindowMicroseconds)
            : 0;

        if (strategy != WaitStrategy.CoarseSleep)
        {
            _timer = new HighResolutionTimer();
            _handles[0] = _timer.Handle;
        }

        _handles[1] = _cancelled.SafeWaitHandle.DangerousGetHandle();
    }

    internal WaitStrategy Strategy => _strategy;

    internal static DeadlineWait CreateForWindows() => new(ProductStrategy);

    internal ScheduleWake WaitUntil(long deadline, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancelled.Reset();
        using CancellationTokenRegistration registration = cancellationToken.UnsafeRegister(
            static state => ((ManualResetEvent)state!).Set(),
            _cancelled);

        while (true)
        {
            long now = MonotonicClock.Timestamp;
            if (now >= deadline)
            {
                return new ScheduleWake(now, deadline, cancelled: false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new ScheduleWake(now, deadline, cancelled: true);
            }

            long remaining = deadline - now;
            if (_strategy == WaitStrategy.Hybrid && remaining <= _spinWindowTicks)
            {
                if (!SpinUntil(deadline, cancellationToken))
                {
                    return new ScheduleWake(MonotonicClock.Timestamp, deadline, cancelled: true);
                }

                continue;
            }

            if (_strategy == WaitStrategy.CoarseSleep)
            {
                CoarseWait(remaining);
            }
            else
            {
                TimerWait(deadline);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
        _cancelled.Dispose();
    }

    private void CoarseWait(long remainingTicks)
    {
        long milliseconds = remainingTicks * 1000L / MonotonicClock.Frequency;
        if (milliseconds < 1)
        {
            milliseconds = 1;
        }
        else if (milliseconds > int.MaxValue)
        {
            milliseconds = int.MaxValue;
        }

        uint result = WaitForSingleObject(_handles[1], (uint)milliseconds);
        if (result == WaitFailed)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
        }
    }

    private void TimerWait(long deadline)
    {
        long now = MonotonicClock.Timestamp;
        long parkDeadline = _strategy == WaitStrategy.Hybrid ? deadline - _spinWindowTicks : deadline;
        if (parkDeadline <= now)
        {
            return;
        }

        _timer!.Arm(MonotonicClock.RelativeDueTime100Ns(parkDeadline - now));
        uint result = WaitForMultipleObjects(2, _handles, false, WaitInfinite);
        if (result == WaitFailed)
        {
            _timer.Cancel();
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "WaitForMultipleObjects failed.");
        }

        if (result != 0 && result != 1 && result != WaitTimeout)
        {
            _timer.Cancel();
            throw new InvalidOperationException($"WaitForMultipleObjects returned {result}.");
        }

        _timer.Cancel();
    }

    private static bool SpinUntil(long deadline, CancellationToken cancellationToken)
    {
        while (MonotonicClock.Timestamp < deadline)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForMultipleObjects(uint count, IntPtr[] handles, bool waitAll, uint milliseconds);
}
