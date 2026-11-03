using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class DesktopTryoutTests
{
    [Fact]
    public void The_tryout_documents_are_ready_to_run()
    {
        Assert.True(MacroRuns.Prepare(DesktopTryout.MoveDocument(10, 20)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.ClickDocument(10, 20, 2)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.HoldDocument("F13", 500)).IsReady);
        Assert.True(MacroRuns.Prepare(DesktopTryout.RepeatClickDocument(10, 20, 1, 3, 200)).IsReady);
    }

    [Fact]
    public void Release_clears_a_held_key_without_sending_real_input()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        using var tryout = DesktopTryout.Create(
            new ExecutionWorker(manager, new DeadlineWait(WaitStrategy.HighResolutionTimer)),
            new VirtualScreen(0, 0, 1920, 1080),
            new ScriptedHook());

        OwnershipResult down = manager.KeyDown(InputKey.F13);
        Assert.Single(down.StillOwned);
        tryout.ReleaseHeldInputs();
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void An_unknown_key_is_refused_before_a_document_is_built()
    {
        Assert.Throws<ArgumentException>(() => DesktopTryout.HoldDocument("NotAKey", 10));
    }

    [Fact]
    public void Recording_leaves_out_this_window_and_playback_uses_the_stand_in()
    {
        var native = new CapturingNative();
        var hook = new ScriptedHook();
        using var tryout = DesktopTryout.Create(
            new ExecutionWorker(new InputStateManager(new SendInputBatch(native)), new DeadlineWait(WaitStrategy.HighResolutionTimer)),
            new VirtualScreen(0, 0, 1920, 1080),
            hook);

        tryout.StartRecording(captureMovement: true);
        hook.Raise(RecordingDecoder.MouseMove, 400, 400, 1);
        hook.Raise(RecordingDecoder.LeftDown, 10, 10, 2);
        hook.Raise(RecordingDecoder.LeftUp, 10, 10, 3);
        tryout.StopRecording(0, 0, 100, 100);

        string log = tryout.TakeLog();
        Assert.Contains("1 actions", log, StringComparison.Ordinal);
        Assert.Contains("Kept in memory", log, StringComparison.Ordinal);

        tryout.PlayRecording();

        Assert.True(SpinWait.SpinUntil(
            () => tryout.State == WorkerState.Stopped.ToString(),
            TimeSpan.FromSeconds(2)));
        Assert.Equal(1, native.Calls);
    }

    [Fact]
    public void Playback_before_a_recording_is_refused()
    {
        var hook = new ScriptedHook();
        using var tryout = DesktopTryout.Create(
            new ExecutionWorker(
                new InputStateManager(new SendInputBatch(new CapturingNative())),
                new DeadlineWait(WaitStrategy.HighResolutionTimer)),
            new VirtualScreen(0, 0, 100, 100),
            hook);

        tryout.PlayRecording();

        Assert.Contains("Record something first.", tryout.TakeLog(), StringComparison.Ordinal);
        Assert.Equal(0, hook.Installs);
    }

    private sealed class ScriptedHook : IRecordingHook
    {
        private Action<HookSample>? _callback;

        internal int Installs { get; private set; }

        public bool Install(Action<HookSample> onSample)
        {
            Installs++;
            _callback = onSample;
            return true;
        }

        public void Uninstall() => _callback = null;

        internal void Raise(int message, int x, int y, long timestamp) =>
            _callback?.Invoke(new HookSample(message, 0, false, timestamp, x, y));
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            return ((uint)inputs.Length, 0);
        }
    }
}
