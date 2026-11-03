using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ClickStitch.Input;

/// <summary>
/// One measured series. Each sample asks for <c>now + interval</c> and submits after the wait.
/// A late sample does not pull the next deadline forward, so the run cannot burst clicks to catch up.
/// </summary>
internal sealed class TimingRun
{
    internal required WaitStrategy Strategy { get; init; }

    internal required int SpinWindowMicroseconds { get; init; }

    internal required int IntervalMilliseconds { get; init; }

    internal required int WarmupSamples { get; init; }

    internal required bool SubmittedPointerMove { get; init; }

    internal required TimingSummary SchedulingError { get; init; }

    internal required TimingSummary SubmissionDuration { get; init; }

    internal required TimingSummary Spacing { get; init; }

    internal required long WallTicks { get; init; }

    internal required TimeSpan ProcessorTime { get; init; }

    internal required long WorkingSetBytes { get; init; }

    internal required int EarlySamples { get; init; }

    internal required int SubmissionFailures { get; init; }

    internal required int CursorStartX { get; init; }

    internal required int CursorStartY { get; init; }

    internal required int CursorEndX { get; init; }

    internal required int CursorEndY { get; init; }

    internal required bool CursorRead { get; init; }
}

internal readonly struct TimingBenchmarkRequest
{
    internal TimingBenchmarkRequest(
        WaitStrategy strategy,
        int intervalMilliseconds,
        int samples,
        int warmupSamples,
        bool submitPointerMove)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intervalMilliseconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(intervalMilliseconds, 3_600_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(samples, 100_000);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupSamples);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(warmupSamples, 100_000);
        Strategy = strategy;
        IntervalMilliseconds = intervalMilliseconds;
        Samples = samples;
        WarmupSamples = warmupSamples;
        SubmitPointerMove = submitPointerMove;
    }

    internal WaitStrategy Strategy { get; }

    internal int IntervalMilliseconds { get; }

    internal int Samples { get; }

    internal int WarmupSamples { get; }

    internal bool SubmitPointerMove { get; }
}

/// <summary>
/// Records scheduling error and <c>SendInput</c> duration.
/// The pointer packet is a relative move of 0,0 so the cursor stays put while the call still runs.
/// </summary>
internal static class TimingBenchmark
{
    internal static TimingRun Run(TimingBenchmarkRequest request)
    {
        if (request.SubmitPointerMove && !Environment.Is64BitProcess)
        {
            throw new InvalidOperationException("Pointer submission is measured only in a 64-bit process.");
        }

        using var wait = new DeadlineWait(request.Strategy);
        using var batch = request.SubmitPointerMove ? new MeasuredBatch() : null;
        return Run(wait, request, batch);
    }

    internal static TimingRun Run(DeadlineWait wait, TimingBenchmarkRequest request, MeasuredBatch? batch)
    {
        ArgumentNullException.ThrowIfNull(wait);
        if (wait.Strategy != request.Strategy)
        {
            throw new ArgumentException("The wait strategy does not match the request.", nameof(wait));
        }

        long intervalTicks = MonotonicClock.TicksFromMilliseconds(request.IntervalMilliseconds);
        var errors = new long[request.Samples];
        var durations = new long[request.Samples];
        var gaps = new long[request.Samples];

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int cursorStartX = 0;
        int cursorStartY = 0;
        bool cursorRead = request.SubmitPointerMove && TryGetCursor(out cursorStartX, out cursorStartY);

        long previousSubmit = Warmup(wait, batch, intervalTicks, request.WarmupSamples);
        int early = 0;
        int failures = 0;

        Process process = Process.GetCurrentProcess();
        TimeSpan processorStart = process.TotalProcessorTime;
        long wallStart = MonotonicClock.Timestamp;

        for (int index = 0; index < request.Samples; index++)
        {
            long requested = MonotonicClock.Timestamp + intervalTicks;
            ScheduleWake wake = wait.WaitUntil(requested, CancellationToken.None);
            if (wake.Cancelled)
            {
                throw new InvalidOperationException("The benchmark wait was cancelled.");
            }

            long before = MonotonicClock.Timestamp;
            long error = before - requested;
            if (error < 0)
            {
                early++;
            }

            long duration = 0;
            if (batch is not null)
            {
                long submitStart = MonotonicClock.Timestamp;
                InputSubmission submission = batch.Submit();
                duration = MonotonicClock.Timestamp - submitStart;
                if (!submission.IsFull)
                {
                    failures++;
                    throw new InvalidOperationException(
                        $"SendInput accepted {submission.Accepted} of {submission.Requested}. Win32 error: {submission.Win32Error}.");
                }
            }

            errors[index] = error;
            durations[index] = duration;
            gaps[index] = before - previousSubmit;
            previousSubmit = before;
        }

        long wallTicks = MonotonicClock.Timestamp - wallStart;
        process.Refresh();
        TimeSpan processorTime = process.TotalProcessorTime - processorStart;
        long workingSet = process.WorkingSet64;
        int cursorEndX = cursorStartX;
        int cursorEndY = cursorStartY;
        if (cursorRead && !TryGetCursor(out cursorEndX, out cursorEndY))
        {
            cursorEndX = cursorStartX;
            cursorEndY = cursorStartY;
            cursorRead = false;
        }

        return new TimingRun
        {
            Strategy = request.Strategy,
            SpinWindowMicroseconds = request.Strategy == WaitStrategy.Hybrid ? DeadlineWait.HybridSpinWindowMicroseconds : 0,
            IntervalMilliseconds = request.IntervalMilliseconds,
            WarmupSamples = request.WarmupSamples,
            SubmittedPointerMove = batch is not null,
            SchedulingError = TimingSummary.Summarize(errors),
            SubmissionDuration = TimingSummary.Summarize(durations),
            Spacing = TimingSummary.Summarize(gaps),
            WallTicks = wallTicks,
            ProcessorTime = processorTime,
            WorkingSetBytes = workingSet,
            EarlySamples = early,
            SubmissionFailures = failures,
            CursorStartX = cursorStartX,
            CursorStartY = cursorStartY,
            CursorEndX = cursorEndX,
            CursorEndY = cursorEndY,
            CursorRead = cursorRead,
        };
    }

    internal static string FormatReport(MachineFacts facts, IReadOnlyList<TimingRun> runs)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(runs);
        var text = new StringBuilder();
        text.AppendLine("ClickStitch timing benchmark");
        text.AppendLine(facts.Text);
        text.AppendLine("Each sample requests now + interval. A late sample does not make the next sample fire early.");
        text.AppendLine("Scheduling error is the pre-submit timestamp minus the requested timestamp.");
        text.AppendLine("Spacing is the pre-submit timestamp minus the previous sample's pre-submit timestamp.");
        text.AppendLine("The pointer packet, when submitted, is a relative move of 0,0.");
        text.AppendLine("A cursor delta on a long run includes any physical movement during that run.");
        text.AppendLine("timeBeginPeriod was not called. Thread priority was left unchanged.");
        text.AppendLine("Progress lines are printed every 5 seconds from another thread. The sample loop does not log.");
        text.AppendLine();

        foreach (TimingRun run in runs)
        {
            AppendRun(text, run);
        }

        return text.ToString();
    }

    private static long Warmup(DeadlineWait wait, MeasuredBatch? batch, long intervalTicks, int samples)
    {
        long previous = MonotonicClock.Timestamp;
        for (int index = 0; index < samples; index++)
        {
            long requested = MonotonicClock.Timestamp + intervalTicks;
            wait.WaitUntil(requested, CancellationToken.None);
            long before = MonotonicClock.Timestamp;
            if (batch is not null)
            {
                InputSubmission submission = batch.Submit();
                if (!submission.IsFull)
                {
                    throw new InvalidOperationException(
                        $"SendInput accepted {submission.Accepted} of {submission.Requested} during warmup. Win32 error: {submission.Win32Error}.");
                }
            }

            previous = before;
        }

        return previous;
    }

    private static void AppendRun(StringBuilder text, TimingRun run)
    {
        string spin = run.Strategy == WaitStrategy.Hybrid
            ? $" spin {run.SpinWindowMicroseconds} us"
            : string.Empty;
        text.AppendLine(
            $"Run: {run.Strategy}{spin}, interval {run.IntervalMilliseconds} ms, warmup {run.WarmupSamples}, samples {run.SchedulingError.Count}, submit {(run.SubmittedPointerMove ? "pointer" : "none")}");
        text.AppendLine($"Wall: {FormatMs(MonotonicClock.Milliseconds(run.WallTicks))} ms");
        double wallSeconds = MonotonicClock.Milliseconds(run.WallTicks) / 1000d;
        double coreFraction = wallSeconds <= 0 ? 0 : run.ProcessorTime.TotalSeconds / wallSeconds;
        text.AppendLine(
            FormattableString.Invariant($"Processor time / wall time: {coreFraction:0.000} (1.000 would be one full core)"));
        text.AppendLine(
            FormattableString.Invariant($"Working set: {run.WorkingSetBytes / 1048576d:0.0} MB"));
        if (run.CursorRead)
        {
            text.AppendLine(
                $"Cursor: ({run.CursorStartX}, {run.CursorStartY}) -> ({run.CursorEndX}, {run.CursorEndY})");
        }

        text.AppendLine("Scheduling error ms: " + Format(run.SchedulingError));
        text.AppendLine("Submission duration ms: " + Format(run.SubmissionDuration));
        text.AppendLine("Spacing ms: " + Format(run.Spacing));
        text.AppendLine($"Early samples: {run.EarlySamples}");
        text.AppendLine($"Submission failures: {run.SubmissionFailures}");
        text.AppendLine();
    }

    private static string Format(TimingSummary summary)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"min {FormatMs(MonotonicClock.Milliseconds(summary.Minimum))}  mean {FormatMs(MonotonicClock.Milliseconds(summary.MeanTicks))}  P50 {FormatMs(MonotonicClock.Milliseconds(summary.P50))}  P95 {FormatMs(MonotonicClock.Milliseconds(summary.P95))}  P99 {FormatMs(MonotonicClock.Milliseconds(summary.P99))}  max {FormatMs(MonotonicClock.Milliseconds(summary.Maximum))}");
    }

    private static string FormatMs(double milliseconds) => milliseconds.ToString("0.000", CultureInfo.InvariantCulture);

    private static bool TryGetCursor(out int x, out int y)
    {
        if (GetCursorPos(out NativePoint point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }
}

/// <summary>
/// Reuses one relative 0,0 move packet so the measured loop does not allocate the batch.
/// </summary>
internal sealed class MeasuredBatch : IDisposable
{
    private readonly SendInputBatch _batch;
    private readonly NativeInput[] _packet;

    internal MeasuredBatch()
    {
        _batch = SendInputBatch.CreateForWindows();
        _packet = GesturePlanner.MoveBy(0, 0).Plan.Packets();
    }

    internal InputSubmission Submit() => _batch.Submit(_packet);

    public void Dispose()
    {
    }
}

internal sealed class MachineFacts
{
    private MachineFacts(string text)
    {
        Text = text;
    }

    internal string Text { get; }

    internal static MachineFacts Capture()
    {
        string configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        string cpu = "unknown";
        string product = "unknown";
        string display = "unknown";
        string build = "unknown";
        string ubr = "unknown";
        using (RegistryKey? processor = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
        {
            if (processor?.GetValue("ProcessorNameString") is string name)
            {
                cpu = name.Trim();
            }
        }

        using (RegistryKey? current = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            product = current?.GetValue("ProductName") as string ?? product;
            display = current?.GetValue("DisplayVersion") as string ?? display;
            build = current?.GetValue("CurrentBuildNumber") as string ?? build;
            if (current?.GetValue("UBR") is int revision)
            {
                ubr = revision.ToString(CultureInfo.InvariantCulture);
            }
        }

        string rtl = TryRtl(out int major, out int minor, out int rtlBuild)
            ? $"{major}.{minor}.{rtlBuild}"
            : "unavailable";

        var text = new StringBuilder();
        text.AppendLine($"CPU: {cpu}");
        text.AppendLine($"Logical processors: {Environment.ProcessorCount}");
        text.AppendLine($"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")} {RuntimeInformation.ProcessArchitecture}");
        text.AppendLine($"Registry ProductName: {product}");
        text.AppendLine($"DisplayVersion: {display}  CurrentBuildNumber: {build}  UBR: {ubr}");
        text.AppendLine($"RtlGetVersion: {rtl}");
        text.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"Configuration: {configuration}");
        text.AppendLine($"Stopwatch frequency: {MonotonicClock.Frequency}  high resolution: {MonotonicClock.IsHighResolution}");
        text.AppendLine($"Thread priority: {Thread.CurrentThread.Priority}");
        return new MachineFacts(text.ToString().TrimEnd());
    }

    private static bool TryRtl(out int major, out int minor, out int build)
    {
        var info = new OsVersionInfo
        {
            Size = (uint)Marshal.SizeOf<OsVersionInfo>(),
            ServicePack = string.Empty,
        };
        if (RtlGetVersion(ref info) == 0)
        {
            major = (int)info.Major;
            minor = (int)info.Minor;
            build = (int)info.Build;
            return true;
        }

        major = 0;
        minor = 0;
        build = 0;
        return false;
    }

    [DllImport("ntdll.dll")]
    private static extern int RtlGetVersion(ref OsVersionInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OsVersionInfo
    {
        internal uint Size;
        internal uint Major;
        internal uint Minor;
        internal uint Build;
        internal uint Platform;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string ServicePack;
    }
}
