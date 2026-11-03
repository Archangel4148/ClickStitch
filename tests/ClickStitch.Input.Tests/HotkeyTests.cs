namespace ClickStitch.Input.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void Proposed_emergency_stop_is_ctrl_alt_shift_f12()
    {
        HotkeyChord emergency = HotkeyBindings.ProposedDefaults.EmergencyStop;

        Assert.Equal(InputKey.F12, emergency.Key);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, emergency.Modifiers);
        Assert.Equal(0x7B, emergency.VirtualKey);
        Assert.Equal(0x4000u | 0x0002u | 0x0001u | 0x0004u, emergency.NativeModifiers);
    }

    [Fact]
    public void Two_roles_cannot_share_a_chord()
    {
        HotkeyChord emergency = HotkeyBindings.ProposedDefaults.EmergencyStop;

        Assert.Throws<ArgumentException>(() => new HotkeyBindings(
            HotkeyBindings.ProposedDefaults.Start,
            HotkeyBindings.ProposedDefaults.PauseResume,
            emergency,
            emergency));
    }

    [Fact]
    public void Registration_uses_no_repeat_and_keeps_the_other_chords_when_one_is_taken()
    {
        var registry = new RecordingHotkeys();
        registry.FailIds.Add((int)HotkeyRole.EmergencyStop);
        var book = new HotkeyBook(registry);

        IReadOnlyList<HotkeyBindingStatus> statuses = book.Register(HotkeyBindings.ProposedDefaults);

        HotkeyBindingStatus emergency = Assert.Single(statuses, static item => item.Role == HotkeyRole.EmergencyStop);
        Assert.False(emergency.Registered);
        Assert.Equal(HotkeyBook.AlreadyRegistered, emergency.Win32Error);
        Assert.Equal(3, registry.Registered.Count);
        Assert.DoesNotContain(registry.Registered, static call => call.Id == (int)HotkeyRole.EmergencyStop);
        Assert.All(registry.Registered, static call => Assert.Equal(HotkeyChord.NoRepeat, call.Modifiers & HotkeyChord.NoRepeat));

        book.Unregister();
        book.Unregister();

        Assert.Equal(3, registry.Unregistered.Count);
        Assert.Empty(book.Registered);
    }

    [Fact]
    public void A_taken_chord_can_be_replaced_and_a_failed_replacement_leaves_that_role_off()
    {
        var registry = new RecordingHotkeys();
        registry.FailIds.Add((int)HotkeyRole.EmergencyStop);
        var book = new HotkeyBook(registry);
        book.Register(HotkeyBindings.ProposedDefaults);
        registry.FailIds.Remove((int)HotkeyRole.EmergencyStop);
        var replacement = new HotkeyChord(InputKey.F8, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);

        HotkeyBindingStatus replaced = book.Replace(HotkeyRole.EmergencyStop, replacement);

        Assert.True(replaced.Registered);
        Assert.Contains((int)HotkeyRole.EmergencyStop, book.Registered.Keys);
        Assert.Equal(0x77u, registry.Registered[^1].VirtualKey);

        registry.FailIds.Add((int)HotkeyRole.EmergencyStop);
        HotkeyBindingStatus failed = book.Replace(HotkeyRole.EmergencyStop, new HotkeyChord(InputKey.F7, HotkeyModifiers.Control));

        Assert.False(failed.Registered);
        Assert.DoesNotContain((int)HotkeyRole.EmergencyStop, book.Registered.Keys);
        Assert.Contains((int)HotkeyRole.Start, book.Registered.Keys);
    }

    [Fact]
    public void A_chord_that_cannot_be_removed_is_not_registered_again()
    {
        var registry = new RecordingHotkeys();
        var book = new HotkeyBook(registry);
        book.Register(HotkeyBindings.ProposedDefaults);
        int calls = registry.Registered.Count;
        registry.FailUnregisterIds.Add((int)HotkeyRole.Start);

        HotkeyBindingStatus status = book.Replace(
            HotkeyRole.Start,
            new HotkeyChord(InputKey.F8, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift));

        Assert.True(status.Registered);
        Assert.Equal(calls, registry.Registered.Count);
        Assert.Contains((int)HotkeyRole.Start, book.Registered.Keys);
    }

    [Fact]
    public void Emergency_stop_does_not_call_start_pause_or_ordinary_stop()
    {
        var registry = new RecordingHotkeys();
        var book = new HotkeyBook(registry);
        var target = new RecordingTarget();
        book.Register(HotkeyBindings.ProposedDefaults);

        book.Dispatch((int)HotkeyRole.EmergencyStop, target);
        book.Dispatch(99, target);

        Assert.Equal(1, target.EmergencyStops);
        Assert.Equal(0, target.Starts);
        Assert.Equal(0, target.Pauses);
        Assert.Equal(0, target.Stops);
    }

    [Fact]
    public void Emergency_stop_releases_a_held_key_while_paused()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        using var wait = new DeadlineWait(WaitStrategy.HighResolutionTimer);
        using var worker = new ExecutionWorker(manager, wait);
        var pressed = new ScriptedHotkeyWait();
        using var listener = new HotkeyListener(new RecordingHotkeys(), new WorkerHotkeyTarget(worker), pressed);
        listener.Start(HotkeyBindings.ProposedDefaults);
        worker.Start(
        [
            WorkerActions.Run(context => context.Inputs.KeyDown(InputKey.A)),
            WorkerActions.Wait(5_000),
        ]);
        WaitUntilHeld(manager);

        pressed.Press((int)HotkeyRole.PauseResume);
        WaitUntil(worker, WorkerState.Paused);
        Assert.Equal([OwnedInput.Key(InputKey.A)], manager.Snapshot());

        pressed.Press((int)HotkeyRole.EmergencyStop);
        WaitUntil(worker, WorkerState.Stopped);

        Assert.Empty(manager.Snapshot());
        listener.Stop();
    }

    [Fact]
    public void Windows_rejects_a_hotkey_with_an_unknown_modifier_flag()
    {
        var registry = new Win32HotkeyRegistry();
        const int id = 0x0B10;

        bool registered = registry.Register(id, 0x1000, 0x70, out int error);
        if (registered)
        {
            registry.Unregister(id, out _);
        }

        Assert.False(registered);
        Assert.Equal(1004, error);
    }

    private static void WaitUntil(ExecutionWorker worker, WorkerState state)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
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
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (manager.Snapshot().Length == 0)
        {
            if (waited.ElapsedMilliseconds > 2_000)
            {
                throw new TimeoutException("The worker did not press the key.");
            }

            Thread.Sleep(5);
        }
    }

    private sealed class RecordingTarget : IHotkeyTarget
    {
        internal int Starts { get; private set; }

        internal int Pauses { get; private set; }

        internal int Stops { get; private set; }

        internal int EmergencyStops { get; private set; }

        public void Start() => Starts++;

        public void PauseOrResume() => Pauses++;

        public void Stop() => Stops++;

        public void EmergencyStop() => EmergencyStops++;
    }

    private sealed class RecordingHotkeys : IHotkeyRegistry
    {
        internal List<(int Id, uint Modifiers, uint VirtualKey)> Registered { get; } = [];

        internal List<int> Unregistered { get; } = [];

        internal HashSet<int> FailIds { get; } = [];

        internal HashSet<int> FailUnregisterIds { get; } = [];

        public bool Register(int id, uint modifiers, uint virtualKey, out int win32Error)
        {
            if (FailIds.Contains(id))
            {
                win32Error = HotkeyBook.AlreadyRegistered;
                return false;
            }

            Registered.Add((id, modifiers, virtualKey));
            win32Error = 0;
            return true;
        }

        public bool Unregister(int id, out int win32Error)
        {
            Unregistered.Add(id);
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
        internal uint? Accepted { get; set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize) =>
            (Accepted ?? (uint)inputs.Length, 0);
    }
}
