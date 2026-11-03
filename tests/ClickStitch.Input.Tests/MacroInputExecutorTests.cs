using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class MacroInputExecutorTests
{
    private static readonly VirtualScreen Screen = new(0, 0, 1920, 1080);

    private static readonly MacroPause Open = new();

    [Fact]
    public void A_macro_submits_each_leaf_action_once_and_tracks_holds()
    {
        var native = new CapturingNative();
        var waits = new List<int>();
        var manager = new InputStateManager(new SendInputBatch(native));
        var executor = new MacroInputExecutor(
            manager,
            (milliseconds, _) =>
            {
                waits.Add(milliseconds);
                return new MacroWaitSlice(milliseconds, false);
            },
            () => Screen,
            Open);
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "play",
              "name": "Play",
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "left", "clickCount": 2, "x": 0, "y": 0 },
                { "id": "nudge", "type": "mouse.move", "coordinateMode": "relative", "x": 5, "y": -3 },
                { "id": "down", "type": "mouse.hold", "button": "left", "state": "down" },
                { "id": "wheel", "type": "mouse.wheel", "notches": 1 },
                { "id": "key", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "pause", "type": "wait", "durationMs": 0 },
                { "id": "key-up", "type": "keyboard.key", "key": "A", "state": "up" },
                { "id": "up", "type": "mouse.hold", "button": "left", "state": "up" },
                { "id": "note", "type": "comment", "text": "ignored" }
              ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(7, native.Calls);
        Assert.Equal([0], waits);
        Assert.Empty(manager.Snapshot());
        Assert.Equal(
            [
                NativeInputConstants.MouseMoveAbsoluteVirtualDesk,
                NativeInputConstants.MouseEventLeftDown,
                NativeInputConstants.MouseEventLeftUp,
                NativeInputConstants.MouseEventLeftDown,
                NativeInputConstants.MouseEventLeftUp,
                NativeInputConstants.MouseEventMove,
                NativeInputConstants.MouseEventLeftDown,
                NativeInputConstants.MouseEventWheel,
                0,
                NativeInputConstants.KeyEventKeyUp,
                NativeInputConstants.MouseEventLeftUp,
            ],
            native.Packets.Select(Flags));
        Assert.Equal(5, native.Packets[5].Data.Mouse.dx);
        Assert.Equal(-3, native.Packets[5].Data.Mouse.dy);
        Assert.Equal(120u, native.Packets[7].Data.Mouse.mouseData);
        Assert.Equal(0x41, native.Packets[8].Data.Keyboard.wVk);
    }

    [Fact]
    public void A_relative_click_and_hold_each_use_one_batch()
    {
        var native = new CapturingNative();
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "drag",
              "name": "Drag",
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "right", "coordinateMode": "relative", "x": 2, "y": 4 },
                { "id": "hold", "type": "mouse.hold", "button": "middle", "state": "down", "coordinateMode": "relative", "x": 1, "y": 1 },
                { "id": "release", "type": "mouse.hold", "button": "middle", "state": "up", "coordinateMode": "primaryDisplay", "x": 8, "y": 9 }
              ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, Create(native, []));

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(3, native.Calls);
        Assert.Equal(3, native.PacketsPerCall[0]);
        Assert.Equal(2, native.PacketsPerCall[1]);
        Assert.Equal(2, native.PacketsPerCall[2]);
        Assert.Equal(NativeInputConstants.MouseEventMove, native.Packets[0].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventRightDown, native.Packets[1].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventRightUp, native.Packets[2].Data.Mouse.dwFlags);
        PointerCoordinates.TryToAbsolute(Screen, 8, 9, out int absoluteX, out int absoluteY);
        Assert.Equal(absoluteX, native.Packets[5].Data.Mouse.dx);
        Assert.Equal(absoluteY, native.Packets[5].Data.Mouse.dy);
    }

    [Fact]
    public void An_arrow_key_sets_the_extended_flag()
    {
        var native = new CapturingNative();
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "arrow",
              "name": "Arrow",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "Left", "state": "down" },
                { "id": "up", "type": "keyboard.key", "key": "Left", "state": "up" }
              ]
            }
            """);

        MacroRunner.Run(contract, Create(native, []));

        Assert.Equal(NativeInputConstants.KeyEventExtendedKey, native.Packets[0].Data.Keyboard.dwFlags);
        Assert.Equal(
            NativeInputConstants.KeyEventExtendedKey | NativeInputConstants.KeyEventKeyUp,
            native.Packets[1].Data.Keyboard.dwFlags);
        Assert.Equal(0x25, native.Packets[0].Data.Keyboard.wVk);
    }

    [Fact]
    public void A_short_batch_stops_the_next_action()
    {
        var native = new CapturingNative { Accepted = 0 };
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "short",
              "name": "Short",
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "left", "coordinateMode": "cursor" },
                { "id": "key", "type": "keyboard.key", "key": "B", "state": "down" }
              ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, Create(native, []));

        Assert.Equal(MacroRunStatus.Failed, result.Status);
        Assert.Equal("SendInput accepted 0 of 2.", Assert.Single(result.Failures).Message);
        Assert.Equal(1, native.Calls);
        Assert.Empty(result.CompletedActionIds);
    }

    [Fact]
    public void A_point_outside_the_screen_is_not_sent()
    {
        var native = new CapturingNative();
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "miss",
              "name": "Miss",
              "actions": [ { "id": "move", "type": "mouse.move", "x": 5000, "y": 10 } ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, Create(native, []));

        Assert.Equal(MacroRunStatus.Failed, result.Status);
        Assert.Equal("The point is outside the virtual screen.", Assert.Single(result.Failures).Message);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void A_cancelled_wait_does_not_complete_and_does_not_start_the_next_action()
    {
        using var cancellation = new CancellationTokenSource();
        var native = new CapturingNative();
        var executor = new MacroInputExecutor(
            new InputStateManager(new SendInputBatch(native)),
            (_, _) =>
            {
                cancellation.Cancel();
                return new MacroWaitSlice(0, true);
            },
            () => Screen,
            Open,
            cancellation.Token);
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "stop-wait",
              "name": "Stop wait",
              "actions": [
                { "id": "pause", "type": "wait", "durationMs": 25 },
                { "id": "click", "type": "mouse.click", "button": "left", "coordinateMode": "cursor" }
              ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, executor, cancellation.Token);

        Assert.Equal(MacroRunStatus.Stopped, result.Status);
        Assert.Empty(result.CompletedActionIds);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void A_repeat_submits_the_child_once_per_count()
    {
        var native = new CapturingNative();
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "repeat-click",
              "name": "Repeat click",
              "actions": [
                {
                  "id": "again",
                  "type": "repeat.count",
                  "count": 2,
                  "actions": [
                    { "id": "click", "type": "mouse.click", "button": "left", "coordinateMode": "cursor" }
                  ]
                }
              ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, Create(native, []));

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(2, native.Calls);
        Assert.Equal([2, 2], native.PacketsPerCall);
        Assert.Single(Assert.IsType<RepeatContract>(contract.Actions[0]).Actions);
    }

    [Fact]
    public void Pause_keeps_a_held_key_and_the_rest_of_a_wait()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        using var pause = new MacroPause();
        var requested = new List<int>();
        var executor = new MacroInputExecutor(
            manager,
            (milliseconds, _) =>
            {
                requested.Add(milliseconds);
                if (requested.Count == 1)
                {
                    pause.Pause();
                    return new MacroWaitSlice(10, true);
                }

                return new MacroWaitSlice(milliseconds, false);
            },
            () => Screen,
            pause);
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "hold-pause",
              "name": "Hold pause",
              "actions": [
                { "id": "down", "type": "keyboard.key", "key": "A", "state": "down" },
                { "id": "pause", "type": "wait", "durationMs": 50 },
                { "id": "up", "type": "keyboard.key", "key": "A", "state": "up" }
              ]
            }
            """);

        MacroRunResult? result = null;
        var thread = new Thread(() => result = MacroRunner.Run(contract, executor, pause: pause)) { IsBackground = true };
        thread.Start();
        Assert.True(SpinWait.SpinUntil(() => pause.IsBlocked, TimeSpan.FromSeconds(2)));
        Assert.Equal([OwnedInput.Key(InputKey.A)], manager.Snapshot());
        Assert.Equal([50], requested);
        pause.Resume();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));

        Assert.Equal(MacroRunStatus.Completed, result!.Status);
        Assert.Equal([50, 40], requested);
        Assert.Equal(2, native.Calls);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void A_relative_move_does_not_ask_for_the_screen()
    {
        var native = new CapturingNative();
        var executor = new MacroInputExecutor(
            new InputStateManager(new SendInputBatch(native)),
            (_, _) => new MacroWaitSlice(0, false),
            () => throw new InvalidOperationException("The screen is not used for a relative move."),
            Open);
        MacroRunContract contract = Compile("""
            {
              "formatVersion": 1,
              "id": "relative",
              "name": "Relative",
              "actions": [ { "id": "move", "type": "mouse.move", "coordinateMode": "relative", "x": 1, "y": 2 } ]
            }
            """);

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal(1, native.Calls);
    }

    private static MacroInputExecutor Create(CapturingNative native, List<int> waits)
    {
        return new MacroInputExecutor(
            new InputStateManager(new SendInputBatch(native)),
            (milliseconds, _) =>
            {
                waits.Add(milliseconds);
                return new MacroWaitSlice(milliseconds, false);
            },
            () => Screen,
            Open);
    }

    private static MacroRunContract Compile(string json)
    {
        MacroReadResult result = MacroJson.Read(json);
        Assert.True(result.IsValid, result.Issues.Count == 0 ? "" : result.Issues[0].Message);
        return MacroExecution.Compile(result.Document!);
    }

    private static uint Flags(NativeInput packet) =>
        packet.type == NativeInputConstants.InputKeyboard ? packet.Data.Keyboard.dwFlags : packet.Data.Mouse.dwFlags;

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<int> PacketsPerCall { get; } = [];

        internal List<NativeInput> Packets { get; } = [];

        internal uint? Accepted { get; set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            PacketsPerCall.Add(inputs.Length);
            Packets.AddRange(inputs);
            return (Accepted ?? (uint)inputs.Length, 0);
        }
    }
}
