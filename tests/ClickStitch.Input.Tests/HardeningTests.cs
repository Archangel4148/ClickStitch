using System.Diagnostics;

namespace ClickStitch.Input.Tests;

public sealed class HardeningTests
{
    [Fact]
    public void Emergency_stop_during_a_click_releases_the_button_and_skips_the_up()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out _, out ScriptedHotkeyWait pressed);
        int stage = 0;
        runtime.Worker.Start(
        [
            WorkerActions.Run(context =>
            {
                context.Inputs.MouseDown(MouseButton.Left);
                stage = 1;
                if (!context.Wait(5_000))
                {
                    return;
                }

                context.Inputs.MouseUp(MouseButton.Left);
                stage = 2;
            }),
        ]);
        WaitUntilHeld(manager);

        pressed.Press((int)HotkeyRole.EmergencyStop);
        WaitUntil(runtime.Worker, WorkerState.Stopped);

        Assert.Equal(1, stage);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Emergency_stop_during_a_held_key_releases_it()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out _, out ScriptedHotkeyWait pressed);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        pressed.Press((int)HotkeyRole.EmergencyStop);
        WaitUntil(runtime.Worker, WorkerState.Stopped);

        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Emergency_stop_during_a_drag_releases_the_button()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out CapturingNative native, out ScriptedHotkeyWait pressed);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context =>
            {
                context.Inputs.MouseDown(MouseButton.Left);
                context.Inputs.MoveBy(8, -4);
                context.Wait(5_000);
            }),
        ]);
        WaitUntilHeld(manager);

        pressed.Press((int)HotkeyRole.EmergencyStop);
        WaitUntil(runtime.Worker, WorkerState.Stopped);

        Assert.Empty(manager.Snapshot());
        Assert.Contains(native.Packets, static packet => packet.type == NativeInputConstants.InputMouse);
    }

    [Fact]
    public void Shutdown_while_paused_releases_the_held_key()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out _, out ScriptedHotkeyWait pressed);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.Shift)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);
        pressed.Press((int)HotkeyRole.PauseResume);
        WaitUntil(runtime.Worker, WorkerState.Paused);

        ShutdownReport report = runtime.Shutdown();

        Assert.Equal(WorkerState.Stopped, report.WorkerState);
        Assert.True(report.InputsReleased);
        Assert.Empty(report.HotkeysStillRegistered);
        Assert.Null(report.HotkeyFault);
    }

    [Fact]
    public void Repeated_emergency_stops_leave_nothing_held()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out _, out ScriptedHotkeyWait pressed);
        for (int iteration = 0; iteration < 20; iteration++)
        {
            runtime.Worker.Start(
            [
                WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.B)),
                WorkerActions.Wait(5_000),
            ]);
            WaitUntilHeld(manager);
            pressed.Press((int)HotkeyRole.EmergencyStop);
            WaitUntil(runtime.Worker, WorkerState.Stopped);
            Assert.Empty(manager.Snapshot());
        }
    }

    [Fact]
    public void Shutdown_during_a_run_refuses_later_work_and_keeps_the_report()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out _, out _);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        ShutdownReport first = runtime.Shutdown();
        ShutdownReport second = runtime.Shutdown();

        Assert.Same(first, second);
        Assert.Equal(WorkerState.Stopped, first.WorkerState);
        Assert.True(first.InputsReleased);
        Assert.Contains(first.Progress, static item => item.Kind == WorkerEventKind.Stopped);
        Assert.Throws<InvalidOperationException>(() => runtime.Worker.Start([WorkerActions.Wait(0)]));
        Assert.Throws<InvalidOperationException>(() => runtime.Hotkeys!.Start(HotkeyBindings.ProposedDefaults));
    }

    [Fact]
    public void An_action_exception_is_on_the_shutdown_report_and_the_key_is_released()
    {
        using InputRuntime runtime = CreateRuntime(out _, out _, out _);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Run(_ => throw new InvalidOperationException("broken action")),
        ]);
        WaitUntil(runtime.Worker, WorkerState.Failed);

        ShutdownReport report = runtime.Shutdown();

        Assert.Equal(WorkerState.Failed, report.WorkerState);
        Assert.True(report.InputsReleased);
        Assert.Contains(report.Progress, static item => item.Kind == WorkerEventKind.Failed && item.Message == "broken action");
    }

    [Fact]
    public void A_failed_release_stays_on_the_shutdown_report()
    {
        using InputRuntime runtime = CreateRuntime(out InputStateManager manager, out CapturingNative native, out _);
        runtime.Worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Run(_ => native.Accepted = 0),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        ShutdownReport report = runtime.Shutdown();

        Assert.Equal(WorkerState.Failed, report.WorkerState);
        Assert.False(report.InputsReleased);
        Assert.Equal([OwnedInput.Key(InputKey.A)], report.StillOwned);
    }

    [Fact]
    public void A_hotkey_exception_is_reported_and_shutdown_still_releases_inputs()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        var worker = new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer));
        var pressed = new ScriptedHotkeyWait();
        var listener = new HotkeyListener(
            new RecordingHotkeys(),
            new WorkerHotkeyTarget(worker, () => throw new InvalidOperationException("start failed")),
            pressed);
        using var runtime = new InputRuntime(worker, listener);
        listener.Start(HotkeyBindings.ProposedDefaults);
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        pressed.Press((int)HotkeyRole.Start);
        WaitUntilFault(listener);

        ShutdownReport report = runtime.Shutdown();

        Assert.Equal("start failed", report.HotkeyFault?.Message);
        Assert.True(report.InputsReleased);
        Assert.Empty(report.HotkeysStillRegistered);
        Assert.Equal(WorkerState.Stopped, report.WorkerState);
    }

    [Fact]
    public void A_hotkey_that_cannot_be_unregistered_stays_on_the_report()
    {
        var registry = new RecordingHotkeys();
        var worker = new ExecutionWorker(
            new InputStateManager(new SendInputBatch(new CapturingNative())),
            new DeadlineWait(WaitStrategy.HighResolutionTimer));
        var listener = new HotkeyListener(registry, new WorkerHotkeyTarget(worker), new ScriptedHotkeyWait());
        using var runtime = new InputRuntime(worker, listener);
        listener.Start(HotkeyBindings.ProposedDefaults);
        registry.FailUnregisterIds.Add((int)HotkeyRole.EmergencyStop);

        ShutdownReport report = runtime.Shutdown();

        Assert.Equal([HotkeyRole.EmergencyStop], report.HotkeysStillRegistered);
    }

    private static InputRuntime CreateRuntime(
        out InputStateManager manager,
        out CapturingNative native,
        out ScriptedHotkeyWait pressed)
    {
        native = new CapturingNative();
        manager = new InputStateManager(new SendInputBatch(native));
        var worker = new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer));
        pressed = new ScriptedHotkeyWait();
        var listener = new HotkeyListener(new RecordingHotkeys(), new WorkerHotkeyTarget(worker), pressed);
        var runtime = new InputRuntime(worker, listener);
        listener.Start(HotkeyBindings.ProposedDefaults);
        return runtime;
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
                throw new TimeoutException("The worker did not press the input.");
            }

            Thread.Sleep(5);
        }
    }

    private static void WaitUntilFault(HotkeyListener listener)
    {
        var waited = Stopwatch.StartNew();
        while (listener.Fault is null)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException("The hotkey listener did not record a fault.");
            }

            Thread.Sleep(5);
        }
    }

    private sealed class RecordingHotkeys : IHotkeyRegistry
    {
        internal HashSet<int> FailUnregisterIds { get; } = [];

        public bool Register(int id, uint modifiers, uint virtualKey, out int win32Error)
        {
            win32Error = 0;
            return true;
        }

        public bool Unregister(int id, out int win32Error)
        {
            if (FailUnregisterIds.Contains(id))
            {
                win32Error = 1400;
                return false;
            }

            win32Error = 0;
            return true;
        }
    }

    private sealed class ScriptedHotkeyWait : IHotkeyWait
    {
        private readonly Queue<int> _ids = new();
        private readonly object _gate = new();
        private bool _stopped;

        internal void Press(int id)
        {
            lock (_gate)
            {
                _ids.Enqueue(id);
                Monitor.Pulse(_gate);
            }
        }

        public int? WaitForHotkey()
        {
            lock (_gate)
            {
                while (_ids.Count == 0 && !_stopped)
                {
                    Monitor.Wait(_gate);
                }

                if (_ids.Count == 0)
                {
                    return null;
                }

                return _ids.Dequeue();
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _stopped = true;
                Monitor.PulseAll(_gate);
            }
        }
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal List<NativeInput> Packets { get; } = [];

        internal uint? Accepted { get; set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Packets.AddRange(inputs);
            return (Accepted ?? (uint)inputs.Length, 0);
        }
    }
}
