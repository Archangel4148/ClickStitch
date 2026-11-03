using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class RecordingCaptureTests
{
    [Fact]
    public void Buttons_and_a_known_key_are_kept_in_order()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        uint a = InputKeys.Describe(InputKey.A).VirtualKey;
        capture.Start();
        hook.Raise(RecordingDecoder.LeftDown, 0, 10);
        hook.Raise(RecordingDecoder.RightDown, 0, 20);
        hook.Raise(RecordingDecoder.MiddleDown, 0, 30);
        hook.Raise(RecordingDecoder.KeyDown, a, 40);
        hook.Raise(RecordingDecoder.KeyUp, a, 50);
        hook.Raise(RecordingDecoder.MiddleUp, 0, 60);
        hook.Raise(RecordingDecoder.RightUp, 0, 70);
        hook.Raise(RecordingDecoder.LeftUp, 0, 80);
        capture.Collect();

        Assert.Equal(8, capture.Events.Count);
        Assert.Equal(
            new[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle },
            capture.Events.Take(3).Select(item => item.Button).ToArray());
        Assert.All(capture.Events.Take(3), item => Assert.True(item.Down));
        Assert.True(capture.Events[3].IsKey);
        Assert.Equal(InputKey.A, capture.Events[3].Key);
        Assert.True(capture.Events[3].Down);
        Assert.False(capture.Events[4].Down);
        Assert.Equal(10, capture.Events[0].Timestamp);
        Assert.Equal(80, capture.Events[7].Timestamp);
        Assert.Equal(RecordingState.Recording, capture.Session.State);
    }

    [Fact]
    public void Movement_is_off_by_default_and_an_unknown_key_is_ignored()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        hook.RaiseMove(10, 20, 1);
        hook.RaiseWheel(0, 2);
        hook.Raise(RecordingDecoder.MouseHorizontalWheel, 0, 3);
        hook.Raise(RecordingDecoder.KeyDown, 0x60, 4);
        hook.Raise(RecordingDecoder.LeftDown, 0, 5);
        capture.Collect();

        Assert.False(capture.Options.CaptureMovement);
        Assert.True(capture.Options.CaptureWheel);
        Assert.Single(capture.Events);
        Assert.Equal(MouseButton.Left, capture.Events[0].Button);
        Assert.Equal(1, capture.Session.Count);
    }

    [Fact]
    public void Movement_keeps_each_new_point_and_its_timestamp()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { CaptureMovement = true });
        capture.Start();
        hook.RaiseMove(-20, 40, 11);
        hook.RaiseMove(-20, 40, 12);
        hook.RaiseMove(21, 40, 13);
        hook.Raise(RecordingDecoder.LeftDown, 0, 14);
        capture.Collect();

        Assert.Equal(3, capture.Events.Count);
        Assert.Equal(RecordedKind.Move, capture.Events[0].Kind);
        Assert.Equal(-20, capture.Events[0].X);
        Assert.Equal(40, capture.Events[0].Y);
        Assert.Equal(11, capture.Events[0].Timestamp);
        Assert.Equal(21, capture.Events[1].X);
        Assert.Equal(13, capture.Events[1].Timestamp);
        Assert.Equal(RecordedKind.Button, capture.Events[2].Kind);
        Assert.True(capture.Events[2].Down);
    }

    [Fact]
    public void The_wheel_keeps_its_delta_and_a_move_does_not_release_a_held_button()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { CaptureMovement = true });
        capture.Start();
        hook.Raise(RecordingDecoder.LeftDown, 0, 1);
        hook.RaiseWheel(120, 2);
        hook.RaiseWheel(-240, 3);
        hook.RaiseWheel(40, 4);
        hook.RaiseMove(3, 4, 5);
        hook.Raise(RecordingDecoder.LeftDown, 0, 6);
        hook.Raise(RecordingDecoder.MouseHorizontalWheel, 0, 7);
        capture.Collect();

        Assert.Equal(5, capture.Events.Count);
        Assert.Equal(RecordedKind.Wheel, capture.Events[1].Kind);
        Assert.Equal(120, capture.Events[1].WheelDelta);
        Assert.Equal(1, capture.Events[1].Notches);
        Assert.Equal(2, capture.Events[1].Timestamp);
        Assert.Equal(-2, capture.Events[2].Notches);
        Assert.Equal(40, capture.Events[3].WheelDelta);
        Assert.Equal(0, capture.Events[3].Notches);
        Assert.Equal(RecordedKind.Move, capture.Events[4].Kind);
    }

    [Fact]
    public void The_wheel_can_be_turned_off()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { CaptureWheel = false });
        capture.Start();
        hook.RaiseWheel(120, 1);
        hook.Raise(RecordingDecoder.LeftDown, 0, 2);
        capture.Collect();

        Assert.Single(capture.Events);
        Assert.Equal(RecordedKind.Button, capture.Events[0].Kind);
    }

    [Fact]
    public void Injected_samples_are_ignored_unless_the_filter_is_off()
    {
        var ignored = new ScriptedHook();
        var filtered = new RecordingCapture(ignored, RecordingOptions.Default with { CaptureMovement = true });
        filtered.Start();
        ignored.Raise(RecordingDecoder.LeftDown, 0, 1, injected: true);
        ignored.RaiseMove(8, 9, 2, injected: true);
        ignored.RaiseWheel(120, 3, injected: true);
        filtered.Collect();
        Assert.Empty(filtered.Events);

        var kept = new ScriptedHook();
        var open = new RecordingCapture(kept, RecordingOptions.Default with
        {
            IgnoreInjected = false,
            CaptureMovement = true,
        });
        open.Start();
        kept.Raise(RecordingDecoder.LeftDown, 0, 1, injected: true);
        kept.RaiseMove(8, 9, 2, injected: true);
        kept.RaiseWheel(120, 3, injected: true);
        open.Collect();
        Assert.Equal(3, open.Events.Count);
        Assert.Equal(RecordedKind.Button, open.Events[0].Kind);
        Assert.Equal(RecordedKind.Move, open.Events[1].Kind);
        Assert.Equal(RecordedKind.Wheel, open.Events[2].Kind);
        Assert.True(open.Events[0].Down);
    }

    [Fact]
    public void Elapsed_time_comes_from_the_monotonic_clock()
    {
        Assert.Equal(1000, RecordingTime.ElapsedMilliseconds(0, MonotonicClock.Frequency));
        Assert.Equal(0, RecordingTime.ElapsedMilliseconds(50, 10));

        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        long stamp = MonotonicClock.Timestamp;
        hook.Raise(RecordingDecoder.LeftDown, 0, stamp);
        capture.Collect();

        Assert.True(capture.StartedAt > 0);
        Assert.True(stamp >= capture.StartedAt);
        Assert.Equal(stamp, capture.Events[0].Timestamp);
        Assert.Equal(RecordingTime.ElapsedMilliseconds(capture.StartedAt, stamp), capture.ElapsedMilliseconds(capture.Events[0]));
        Assert.InRange(capture.ElapsedMilliseconds(capture.Events[0]), 0, 5_000);
    }

    [Fact]
    public void A_repeated_down_is_ignored_until_the_button_comes_up()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        uint a = InputKeys.Describe(InputKey.A).VirtualKey;
        capture.Start();
        hook.Raise(RecordingDecoder.KeyDown, a, 1);
        hook.Raise(RecordingDecoder.KeyDown, a, 2);
        hook.Raise(RecordingDecoder.KeyUp, a, 3);
        hook.Raise(RecordingDecoder.KeyDown, a, 4);
        capture.Collect();

        Assert.Equal(3, capture.Events.Count);
        Assert.Equal(new[] { true, false, true }, capture.Events.Select(item => item.Down).ToArray());
    }

    [Fact]
    public void The_limit_stops_the_session_and_uninstalls_the_hook()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        for (int i = 0; i < RecordingSession.MaximumEvents + 5; i++)
        {
            hook.Raise(i % 2 == 0 ? RecordingDecoder.LeftDown : RecordingDecoder.LeftUp, 0, i);
        }

        capture.Collect();

        Assert.Equal(RecordingSession.MaximumEvents, capture.Session.Count);
        Assert.Equal(RecordingSession.MaximumEvents, capture.Events.Count);
        Assert.Equal(RecordingState.Stopped, capture.Session.State);
        Assert.Equal(RecordingStop.Limit, capture.Session.StopReason);
        Assert.Equal("Recording stopped after 100,000 events.", capture.Session.Notice);
        Assert.Equal(1, hook.Uninstalls);
    }

    [Fact]
    public void Stop_keeps_the_events_and_a_second_stop_is_refused()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        hook.Raise(RecordingDecoder.LeftDown, 0, 1);
        hook.Raise(RecordingDecoder.LeftUp, 0, 2);
        capture.Stop();

        Assert.Equal(2, capture.Events.Count);
        Assert.Equal(2, capture.Session.Count);
        Assert.Equal(RecordingStop.Requested, capture.Session.StopReason);
        Assert.Equal(1, hook.Uninstalls);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => capture.Stop());
        Assert.Equal("There is no recording to stop.", error.Message);
    }

    [Fact]
    public void Cancel_drops_the_events()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        hook.Raise(RecordingDecoder.LeftDown, 0, 1);
        capture.Cancel();

        Assert.Empty(capture.Events);
        Assert.Equal(0, capture.Session.Count);
        Assert.Equal(RecordingState.Cancelled, capture.Session.State);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => capture.Cancel());
        Assert.Equal("There is no recording to cancel.", error.Message);
    }

    [Fact]
    public void A_failed_install_does_not_leave_the_session_recording()
    {
        var hook = new ScriptedHook { Fail = true };
        var capture = new RecordingCapture(hook);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => capture.Start());

        Assert.Equal("The recording hook could not be installed.", error.Message);
        Assert.Equal(RecordingState.Cancelled, capture.Session.State);
        Assert.Equal(0, capture.Session.Count);
        Assert.Equal(1, hook.Uninstalls);
        Assert.Null(hook.Callback);
    }

    [Fact]
    public void Simplifying_keeps_the_raw_list_and_revert_drops_the_copy()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { CaptureMovement = true });
        capture.Start();
        hook.RaiseMove(0, 0, 1);
        hook.RaiseMove(10, 0, 2);
        hook.RaiseMove(20, 0, 3);
        hook.Raise(RecordingDecoder.LeftDown, 0, 4);

        InvalidOperationException early = Assert.Throws<InvalidOperationException>(() => capture.Simplify());
        Assert.Equal("Stop the recording before simplifying it.", early.Message);

        capture.Stop();
        Assert.Null(capture.Simplified);
        capture.Simplify(new MovementOptimization { Tolerance = 1 });

        Assert.Equal(4, capture.Events.Count);
        Assert.Equal(4, capture.Session.Count);
        Assert.Equal(3, capture.Simplified!.Count);
        Assert.Equal(RecordedKind.Button, capture.Simplified[2].Kind);

        capture.Revert();
        Assert.Null(capture.Simplified);
        Assert.Equal(4, capture.Events.Count);

        capture.Simplify(new MovementOptimization { Tolerance = 1 });
        ArgumentOutOfRangeException rejected = Assert.Throws<ArgumentOutOfRangeException>(() =>
            capture.Simplify(new MovementOptimization { Tolerance = -1 }));
        Assert.Equal("Tolerance", rejected.ParamName);
        Assert.Equal(3, capture.Simplified!.Count);
        capture.Revert();
        capture.Revert();
        Assert.Null(capture.Simplified);
    }

    [Fact]
    public void Saving_uses_the_shortened_copy_and_refuses_a_live_recording()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { CaptureMovement = true });
        capture.Start();
        hook.RaiseMove(0, 0, 1);
        hook.RaiseMove(10, 0, 2);
        hook.RaiseMove(20, 0, 3);
        InvalidOperationException early = Assert.Throws<InvalidOperationException>(() => capture.ToDocument("Line", "rec-line"));
        Assert.Equal("Stop the recording before saving it.", early.Message);

        capture.Stop();
        capture.Simplify(new MovementOptimization { Tolerance = 1 });
        RecordedDocument recorded = capture.ToDocument("Line", "rec-line");

        Assert.True(recorded.IsReady);
        Assert.Equal(2, recorded.Document!.Actions.OfType<MouseMoveAction>().Count());
        Assert.Equal(3, capture.Events.Count);
    }

    [Fact]
    public void An_invalid_sample_interval_refuses_to_start()
    {
        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook, RecordingOptions.Default with { SampleMilliseconds = 60_001 });

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => capture.Start());

        Assert.Equal("SampleMilliseconds", error.ParamName);
        Assert.Equal(RecordingState.Idle, capture.Session.State);
        Assert.Null(hook.Callback);
        Assert.Equal(0, hook.Uninstalls);
    }

    [Fact]
    public void Simplify_before_a_recording_is_refused()
    {
        var idle = new RecordingCapture(new ScriptedHook());
        InvalidOperationException idleError = Assert.Throws<InvalidOperationException>(() => idle.Simplify());
        Assert.Equal("There is no recording to simplify.", idleError.Message);

        var hook = new ScriptedHook();
        var capture = new RecordingCapture(hook);
        capture.Start();
        capture.Cancel();
        InvalidOperationException cancelled = Assert.Throws<InvalidOperationException>(() => capture.Simplify());
        Assert.Equal("There is no recording to simplify.", cancelled.Message);
    }

    [Fact]
    public void Uninstall_before_install_is_safe()
    {
        var hook = new Win32RecordingHook();

        hook.Uninstall();
        hook.Uninstall();
    }

    [Fact]
    public void Injected_flags_match_the_low_level_hook_bits()
    {
        Assert.True(RecordingDecoder.IsKeyboardInjected(RecordingDecoder.KeyboardInjected));
        Assert.False(RecordingDecoder.IsKeyboardInjected(0));
        Assert.True(RecordingDecoder.IsMouseInjected(RecordingDecoder.MouseInjected));
        Assert.True(RecordingDecoder.IsMouseInjected(RecordingDecoder.MouseInjectedFromLowerIntegrity));
        Assert.False(RecordingDecoder.IsMouseInjected(0));
    }

    [Fact]
    public void A_system_key_uses_the_same_schema_key()
    {
        uint alt = InputKeys.Describe(InputKey.Alt).VirtualKey;
        bool decoded = RecordingDecoder.TryDecode(
            new HookSample(RecordingDecoder.SysKeyDown, alt, false, 7),
            RecordingOptions.Default,
            out RecordedEvent recorded);

        Assert.True(decoded);
        Assert.True(recorded.IsKey);
        Assert.Equal(InputKey.Alt, recorded.Key);
        Assert.True(recorded.Down);
        Assert.Equal(7, recorded.Timestamp);
    }

    private sealed class ScriptedHook : IRecordingHook
    {
        internal Action<HookSample>? Callback;

        internal int Uninstalls { get; private set; }

        internal bool Fail { get; init; }

        public bool Install(Action<HookSample> onSample)
        {
            if (Fail)
            {
                return false;
            }

            Callback = onSample;
            return true;
        }

        public void Uninstall()
        {
            Uninstalls++;
            Callback = null;
        }

        internal void Raise(int message, uint virtualKey, long timestamp, bool injected = false)
        {
            Callback?.Invoke(new HookSample(message, virtualKey, injected, timestamp));
        }

        internal void RaiseMove(int x, int y, long timestamp, bool injected = false)
        {
            Callback?.Invoke(new HookSample(RecordingDecoder.MouseMove, 0, injected, timestamp, x, y));
        }

        internal void RaiseWheel(int delta, long timestamp, bool injected = false)
        {
            Callback?.Invoke(new HookSample(RecordingDecoder.MouseWheel, 0, injected, timestamp, wheelDelta: delta));
        }
    }
}
