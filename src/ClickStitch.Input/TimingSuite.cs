namespace ClickStitch.Input;

/// <summary>
/// The opt-in pointer-move benchmark: back-to-back submission, then 1 ms, 10 ms, and 50 ms
/// on each wait strategy. About four minutes. The measured loop does not log.
/// </summary>
internal static class TimingSuite
{
    internal const int Samples = 1000;
    internal const int Warmup = 50;

    internal static IReadOnlyList<TimingRun> Run(Action<string> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (!Environment.Is64BitProcess)
        {
            throw new InvalidOperationException("The timing benchmark runs in a 64-bit process.");
        }

        if (Thread.CurrentThread.Priority != ThreadPriority.Normal)
        {
            throw new InvalidOperationException(
                $"Thread priority is {Thread.CurrentThread.Priority}. The baseline leaves it at Normal.");
        }

        if (!MonotonicClock.IsHighResolution)
        {
            throw new InvalidOperationException("Stopwatch is not high resolution on this machine.");
        }

        (WaitStrategy Strategy, int IntervalMilliseconds)[] plan =
        [
            (WaitStrategy.Hybrid, 0),
            (WaitStrategy.CoarseSleep, 1),
            (WaitStrategy.CoarseSleep, 10),
            (WaitStrategy.CoarseSleep, 50),
            (WaitStrategy.HighResolutionTimer, 1),
            (WaitStrategy.HighResolutionTimer, 10),
            (WaitStrategy.HighResolutionTimer, 50),
            (WaitStrategy.Hybrid, 1),
            (WaitStrategy.Hybrid, 10),
            (WaitStrategy.Hybrid, 50),
        ];

        using var reporter = new RunProgress(progress);
        reporter.Announce(
            "Timing benchmark: 10 runs, about 4 minutes. Each sample is a relative mouse move of 0,0. No clicks.");
        var runs = new List<TimingRun>(plan.Length);
        for (int index = 0; index < plan.Length; index++)
        {
            (WaitStrategy strategy, int interval) = plan[index];
            string label = interval == 0 ? $"{strategy}, back-to-back" : $"{strategy}, {interval} ms";
            reporter.Start(index + 1, plan.Length, label);
            runs.Add(TimingBenchmark.Run(new TimingBenchmarkRequest(strategy, interval, Samples, Warmup, submitPointerMove: true)));
            reporter.Finish();
        }

        return runs;
    }

    internal static void EnsureMeasured(IReadOnlyList<TimingRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        if (runs.Count == 0)
        {
            throw new InvalidOperationException("The benchmark produced no runs.");
        }

        foreach (TimingRun run in runs)
        {
            if (run.SchedulingError.Count != Samples || run.EarlySamples != 0 || run.SubmissionFailures != 0)
            {
                throw new InvalidOperationException(
                    $"{run.Strategy} at {run.IntervalMilliseconds} ms recorded {run.SchedulingError.Count} samples, {run.EarlySamples} early, {run.SubmissionFailures} submission failures.");
            }

            if (!run.SubmittedPointerMove || run.SchedulingError.Minimum < 0)
            {
                throw new InvalidOperationException(
                    $"{run.Strategy} at {run.IntervalMilliseconds} ms submitted no pointer move or woke early.");
            }

            long minimumWall = MonotonicClock.TicksFromMilliseconds(run.IntervalMilliseconds) * Samples;
            if (run.WallTicks + MonotonicClock.TicksFromMilliseconds(50) < minimumWall)
            {
                throw new InvalidOperationException(
                    $"{run.Strategy} at {run.IntervalMilliseconds} ms finished sooner than the requested waits allow.");
            }
        }
    }
}
