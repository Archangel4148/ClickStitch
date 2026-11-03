using System.Diagnostics;

namespace ClickStitch.Input.Tests;

public sealed class ExecutionWorkerTests
{
    [Fact]
    public void A_new_worker_is_idle_and_stop_is_safe_to_repeat()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out _);

        worker.Stop();
        worker.Stop();

        Assert.Equal(WorkerState.Idle, worker.State);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Actions_run_on_the_worker_thread_and_then_the_worker_stops()
    {
        using ExecutionWorker worker = CreateWorker(out _, out _);
        int workerThread = 0;

        worker.Start(
        [
            WorkerActions.Run(_ => workerThread = Environment.CurrentManagedThreadId),
        ]);
        WaitUntil(worker, WorkerState.Stopped);

        Assert.NotEqual(0, workerThread);
        Assert.NotEqual(Environment.CurrentManagedThreadId, workerThread);
        Assert.Contains(worker.DrainProgress(), static item => item.Kind == WorkerEventKind.Stopped);
    }

    [Fact]
    public void Stop_during_a_wait_releases_a_held_key_and_skips_the_next_action()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out CapturingNative native);
        int secondAction = 0;
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Wait(5_000),
            WorkerActions.Run(_ => Interlocked.Increment(ref secondAction)),
        ]);
        WaitUntilHeld(manager);

        worker.Stop();

        Assert.Equal(WorkerState.Stopped, worker.State);
        Assert.Empty(manager.Snapshot());
        Assert.Equal(0, secondAction);
        Assert.Contains(native.Packets, static packet => packet.type == NativeInputConstants.InputKeyboard);
    }

    [Fact]
    public void Pause_keeps_the_held_key_and_does_not_press_it_again()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out CapturingNative native);
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.Shift)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        worker.Pause();
        int callsWhilePaused = native.Calls;
        Thread.Sleep(40);

        Assert.Equal(WorkerState.Paused, worker.State);
        Assert.Equal([OwnedInput.Key(InputKey.Shift)], manager.Snapshot());
        Assert.Equal(callsWhilePaused, native.Calls);

        worker.Resume();
        Thread.Sleep(20);
        Assert.Equal(callsWhilePaused, native.Calls);
        Assert.Equal([OwnedInput.Key(InputKey.Shift)], manager.Snapshot());

        worker.Stop();
        Assert.Equal(WorkerState.Stopped, worker.State);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Pause_keeps_the_remaining_wait()
    {
        using ExecutionWorker worker = CreateWorker(out _, out _);
        long waitStarted = 0;
        long waitFinished = 0;
        worker.Start(
        [
            WorkerActions.Run(context =>
            {
                waitStarted = MonotonicClock.Timestamp;
                context.Report("in-wait");
                context.Wait(300);
                waitFinished = MonotonicClock.Timestamp;
            }),
        ]);
        WaitUntilNoted(worker, "in-wait");
        Thread.Sleep(120);
        worker.Pause();
        long pauseStarted = MonotonicClock.Timestamp;
        Thread.Sleep(200);
        worker.Resume();
        long pauseFinished = MonotonicClock.Timestamp;
        WaitUntil(worker, WorkerState.Stopped);

        long activeTicks = (waitFinished - waitStarted) - (pauseFinished - pauseStarted);
        double activeMilliseconds = MonotonicClock.Milliseconds(activeTicks);
        Assert.InRange(activeMilliseconds, 250, 370);
    }

    [Fact]
    public void An_action_exception_releases_the_held_key_and_fails_the_worker()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out _);
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Run(_ => throw new InvalidOperationException("boom")),
        ]);
        WaitUntil(worker, WorkerState.Failed);

        Assert.Empty(manager.Snapshot());
        Assert.Contains(worker.DrainProgress(), static item => item.Kind == WorkerEventKind.Failed && item.Message == "boom");
    }

    [Fact]
    public void A_release_windows_does_not_accept_fails_the_worker_and_blocks_the_next_start()
    {
        using ExecutionWorker worker = CreateWorker(out InputStateManager manager, out CapturingNative native);
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Run(_ => native.Accepted = 0),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        worker.Stop();

        Assert.Equal(WorkerState.Failed, worker.State);
        Assert.Equal([OwnedInput.Key(InputKey.A)], manager.Snapshot());
        Assert.Throws<InvalidOperationException>(() => worker.Start([WorkerActions.Wait(0)]));
    }

    [Fact]
    public void A_full_progress_queue_drops_the_oldest_event()
    {
        using ExecutionWorker worker = CreateWorker(out _, out _, progressCapacity: 1);
        worker.Start(
        [
            WorkerActions.Run(context => context.Report("one")),
            WorkerActions.Run(context => context.Report("two")),
        ]);
        WaitUntil(worker, WorkerState.Stopped);

        Assert.True(worker.ProgressDrops > 0);
        WorkerEvent[] events = worker.DrainProgress();
        Assert.Single(events);
    }

    [Fact]
    public void Starting_while_running_is_refused()
    {
        using ExecutionWorker worker = CreateWorker(out _, out _);
        worker.Start([WorkerActions.Wait(5_000)]);
        WaitUntil(worker, WorkerState.Running);

        Assert.Throws<InvalidOperationException>(() => worker.Start([WorkerActions.Wait(0)]));

        worker.Stop();
        Assert.Equal(WorkerState.Stopped, worker.State);
    }

    [Fact]
    public void A_second_worker_cannot_bind_the_same_input_state()
    {
        var manager = new InputStateManager(new SendInputBatch(new CapturingNative()));
        using var firstWait = new DeadlineWait(WaitStrategy.HighResolutionTimer);
        using var secondWait = new DeadlineWait(WaitStrategy.HighResolutionTimer);
        using var first = new ExecutionWorker(manager, firstWait);

        Assert.Throws<InvalidOperationException>(() => new ExecutionWorker(manager, secondWait));
    }

    [Fact]
    public void The_windows_factory_is_idle_until_it_is_started()
    {
        using ExecutionWorker worker = ExecutionWorker.CreateForWindows();

        Assert.Equal(WorkerState.Idle, worker.State);
        worker.Stop();
        Assert.Equal(WorkerState.Idle, worker.State);
    }

    private static ExecutionWorker CreateWorker(
        out InputStateManager manager,
        out CapturingNative native,
        int progressCapacity = 32)
    {
        native = new CapturingNative();
        manager = new InputStateManager(new SendInputBatch(native));
        return new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer), progressCapacity);
    }

    private static void WaitUntil(ExecutionWorker worker, WorkerState state)
    {
        var waited = Stopwatch.StartNew();
        while (worker.State != state)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException($"Worker stayed {worker.State}.");
            }

            Thread.Sleep(5);
        }
    }

    private static void WaitUntilHeld(InputStateManager manager)
    {
        var waited = Stopwatch.StartNew();
        while (manager.Snapshot().Length == 0)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException("The worker did not press the key.");
            }

            Thread.Sleep(5);
        }
    }

    private static void WaitUntilNoted(ExecutionWorker worker, string message)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            if (worker.DrainProgress().Any(item => item.Message == message))
            {
                return;
            }

            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException("The worker did not report the wait.");
            }

            Thread.Sleep(5);
        }
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<NativeInput> Packets { get; } = [];

        internal uint? Accepted { get; set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            Packets.AddRange(inputs);
            return (Accepted ?? (uint)inputs.Length, 0);
        }
    }
}
