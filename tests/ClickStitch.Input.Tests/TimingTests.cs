namespace ClickStitch.Input.Tests;

public sealed class TimingTests
{
    [Fact]
    public void One_second_is_one_frequency_period()
    {
        Assert.Equal(0, MonotonicClock.TicksFromMilliseconds(0));
        Assert.Equal(MonotonicClock.Frequency, MonotonicClock.TicksFromMilliseconds(1000));
        Assert.True(MonotonicClock.IsHighResolution);
    }

    [Fact]
    public void Millisecond_conversion_rounds_up()
    {
        long one = MonotonicClock.TicksFromMilliseconds(1);
        long two = MonotonicClock.TicksFromMilliseconds(2);

        Assert.True(one > 0);
        Assert.True(two > one);
        Assert.Throws<ArgumentOutOfRangeException>(() => MonotonicClock.TicksFromMilliseconds(-1));
    }

    [Fact]
    public void One_millisecond_is_ten_thousand_timer_units()
    {
        long due = MonotonicClock.RelativeDueTime100Ns(MonotonicClock.TicksFromMilliseconds(1));

        Assert.InRange(due, -10_001, -10_000);
    }

    [Fact]
    public void Percentiles_use_nearest_rank()
    {
        long[] values = [3, 1, 2];
        TimingSummary summary = TimingSummary.Summarize(values);

        Assert.Equal(3, summary.Count);
        Assert.Equal(1, summary.Minimum);
        Assert.Equal(2, summary.P50);
        Assert.Equal(3, summary.Maximum);
        Assert.Equal(2, summary.MeanTicks);
    }

    [Fact]
    public void A_single_sample_is_every_percentile()
    {
        TimingSummary summary = TimingSummary.Summarize([7]);

        Assert.Equal(7, summary.Minimum);
        Assert.Equal(7, summary.P50);
        Assert.Equal(7, summary.P95);
        Assert.Equal(7, summary.P99);
        Assert.Equal(7, summary.Maximum);
    }

    [Fact]
    public void Known_ranks_for_one_hundred_samples()
    {
        var values = new long[100];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = index;
        }

        TimingSummary summary = TimingSummary.Summarize(values);

        Assert.Equal(49, summary.P50);
        Assert.Equal(94, summary.P95);
        Assert.Equal(98, summary.P99);
        Assert.Equal(99, summary.Maximum);
    }

    [Fact]
    public void A_wait_does_not_return_before_the_deadline()
    {
        AssertNeverEarly(WaitStrategy.CoarseSleep);
        AssertNeverEarly(WaitStrategy.HighResolutionTimer);
        AssertNeverEarly(WaitStrategy.Hybrid);
    }

    [Fact]
    public void A_deadline_already_due_returns_immediately()
    {
        using var wait = new DeadlineWait(WaitStrategy.Hybrid);
        long deadline = MonotonicClock.Timestamp - MonotonicClock.TicksFromMilliseconds(5);
        long started = MonotonicClock.Timestamp;

        ScheduleWake wake = wait.WaitUntil(deadline, CancellationToken.None);

        Assert.False(wake.Cancelled);
        Assert.True(wake.SchedulingErrorTicks > 0);
        Assert.True(MonotonicClock.Timestamp - started < MonotonicClock.TicksFromMilliseconds(250));
    }

    [Fact]
    public void Cancellation_ends_a_wait()
    {
        AssertCancellationEndsTheWait(WaitStrategy.CoarseSleep);
        AssertCancellationEndsTheWait(WaitStrategy.HighResolutionTimer);
        AssertCancellationEndsTheWait(WaitStrategy.Hybrid);
    }

    [Fact]
    public void Machine_facts_can_be_read()
    {
        MachineFacts facts = MachineFacts.Capture();

        Assert.Contains("Runtime:", facts.Text, StringComparison.Ordinal);
        Assert.Contains("Thread priority:", facts.Text, StringComparison.Ordinal);
    }

    private static void AssertNeverEarly(WaitStrategy strategy)
    {
        using var wait = new DeadlineWait(strategy);
        long interval = MonotonicClock.TicksFromMilliseconds(2);

        for (int index = 0; index < 5; index++)
        {
            long requested = MonotonicClock.Timestamp + interval;
            ScheduleWake wake = wait.WaitUntil(requested, CancellationToken.None);

            Assert.False(wake.Cancelled);
            Assert.True(wake.Timestamp >= requested);
            Assert.True(wake.SchedulingErrorTicks >= 0);
        }
    }

    private static void AssertCancellationEndsTheWait(WaitStrategy strategy)
    {
        using var wait = new DeadlineWait(strategy);
        using var cancelled = new CancellationTokenSource();
        long deadline = MonotonicClock.Timestamp + MonotonicClock.TicksFromMilliseconds(2000);
        var completed = new TaskCompletionSource<ScheduleWake>(TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = new Thread(() =>
        {
            try
            {
                completed.SetResult(wait.WaitUntil(deadline, cancelled.Token));
            }
            catch (Exception error)
            {
                completed.SetException(error);
            }
        });
        worker.IsBackground = true;
        worker.Start();
        cancelled.Cancel();

        Assert.True(completed.Task.Wait(TimeSpan.FromSeconds(3)));
        Assert.True(completed.Task.Result.Cancelled);
        Assert.True(worker.Join(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void The_harness_records_a_short_wait_without_submitting_input()
    {
        TimingRun run = TimingBenchmark.Run(new TimingBenchmarkRequest(
            WaitStrategy.HighResolutionTimer,
            intervalMilliseconds: 1,
            samples: 8,
            warmupSamples: 2,
            submitPointerMove: false));

        Assert.Equal(8, run.SchedulingError.Count);
        Assert.Equal(0, run.EarlySamples);
        Assert.Equal(0, run.SubmissionFailures);
        Assert.False(run.SubmittedPointerMove);
        Assert.Equal(0, run.SubmissionDuration.Maximum);
        Assert.True(run.SchedulingError.Minimum >= 0);
        Assert.True(run.SchedulingError.P50 <= run.SchedulingError.P95);
        Assert.True(run.SchedulingError.P95 <= run.SchedulingError.P99);
        Assert.True(run.SchedulingError.P99 <= run.SchedulingError.Maximum);
        Assert.Equal(8, run.Spacing.Count);
    }

    [Fact]
    public void Progress_lines_mark_the_start_and_end_of_a_run()
    {
        var lines = new List<string>();
        using var progress = new RunProgress(lines.Add);

        progress.Announce("starting");
        progress.Start(2, 10, "Hybrid, 50 ms");
        progress.Finish();

        Assert.Equal("starting", lines[0]);
        Assert.Equal("[2/10] Hybrid, 50 ms", lines[1]);
        Assert.StartsWith("[2/10] finished in ", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_product_wait_is_hybrid()
    {
        Assert.Equal(WaitStrategy.Hybrid, DeadlineWait.ProductStrategy);
        using var wait = DeadlineWait.CreateForWindows();
        Assert.Equal(WaitStrategy.Hybrid, wait.Strategy);
    }
}
