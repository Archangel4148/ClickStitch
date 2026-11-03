using ClickStitch.Core;

namespace ClickStitch.Input.Tests;

public sealed class RecordingCompilerTests
{
    [Fact]
    public void Schema_key_names_follow_the_input_key_order()
    {
        InputKey[] keys = Enum.GetValues<InputKey>();
        Assert.Equal(MacroKeys.All.Count, keys.Length);
        foreach (InputKey key in keys)
        {
            Assert.Equal(key.ToString(), MacroKeys.All[(int)key]);
        }
    }

    [Fact]
    public void A_click_keeps_the_move_and_the_gaps_around_the_press()
    {
        long down = MonotonicClock.TicksFromMilliseconds(80);
        long up = MonotonicClock.TicksFromMilliseconds(100);
        long key = MonotonicClock.TicksFromMilliseconds(140);
        long release = MonotonicClock.TicksFromMilliseconds(160);
        RecordedDocument recorded = RecordingCompiler.Compile(
            [
                RecordedEvent.Move(0, 15, 25),
                RecordedEvent.Mouse(down, MouseButton.Left, true),
                RecordedEvent.Mouse(up, MouseButton.Left, false),
                RecordedEvent.KeyPress(key, InputKey.A, true),
                RecordedEvent.KeyPress(release, InputKey.A, false),
            ],
            "Sample",
            "rec-1");

        Assert.True(recorded.IsReady);
        Assert.Empty(recorded.Notices);
        Assert.Equal(
            [
                typeof(MouseMoveAction),
                typeof(WaitAction),
                typeof(MouseClickAction),
                typeof(WaitAction),
                typeof(KeyboardKeyAction),
                typeof(WaitAction),
                typeof(KeyboardKeyAction),
            ],
            recorded.Document!.Actions.Select(action => action.GetType()).ToArray());
        var move = (MouseMoveAction)recorded.Document.Actions[0];
        Assert.Equal(15, move.X);
        Assert.Equal(25, move.Y);
        Assert.Equal(MacroCoordinateMode.VirtualDesktop, move.Mode);
        Assert.Equal(RecordingTime.ElapsedMilliseconds(0, down), ((WaitAction)recorded.Document.Actions[1]).DurationMs);
        var click = (MouseClickAction)recorded.Document.Actions[2];
        Assert.Equal(MacroMouseButton.Left, click.Button);
        Assert.Equal(MacroCoordinateMode.Cursor, click.Mode);
        Assert.Equal(1, click.ClickCount);
        Assert.Equal(RecordingTime.ElapsedMilliseconds(up, key), ((WaitAction)recorded.Document.Actions[3]).DurationMs);
        Assert.Equal("A", ((KeyboardKeyAction)recorded.Document.Actions[4]).Key);
        Assert.Equal(MacroPressState.Down, ((KeyboardKeyAction)recorded.Document.Actions[4]).State);
        Assert.Equal(MacroPressState.Up, ((KeyboardKeyAction)recorded.Document.Actions[6]).State);
    }

    [Fact]
    public void A_drag_is_a_hold_around_the_moves()
    {
        RecordedDocument recorded = RecordingCompiler.Compile(
            [
                RecordedEvent.Mouse(1, MouseButton.Left, true),
                RecordedEvent.Move(2, 4, 8),
                RecordedEvent.Move(3, 9, 8),
                RecordedEvent.Mouse(4, MouseButton.Left, false),
            ],
            "Drag",
            "rec-drag");

        Assert.True(recorded.IsReady);
        Assert.Equal(
            [typeof(MouseHoldAction), typeof(MouseMoveAction), typeof(MouseMoveAction), typeof(MouseHoldAction)],
            recorded.Document!.Actions.Select(action => action.GetType()).ToArray());
        Assert.Equal(MacroPressState.Down, ((MouseHoldAction)recorded.Document.Actions[0]).State);
        Assert.Equal(MacroPressState.Up, ((MouseHoldAction)recorded.Document.Actions[3]).State);
        Assert.Empty(recorded.Notices);
    }

    [Fact]
    public void Partial_wheel_units_add_up_and_a_large_delta_splits()
    {
        RecordedDocument parts = RecordingCompiler.Compile(
            [RecordedEvent.Wheel(1, 40), RecordedEvent.Wheel(2, 80)],
            "Wheel",
            "rec-wheel");
        Assert.True(parts.IsReady);
        Assert.Empty(parts.Notices);
        var notch = Assert.IsType<MouseWheelAction>(Assert.Single(parts.Document!.Actions));
        Assert.Equal(1, notch.Notches);

        RecordedDocument leftover = RecordingCompiler.Compile(
            [RecordedEvent.Wheel(1, 40)],
            "Wheel",
            "rec-wheel");
        Assert.Empty(leftover.Document!.Actions);
        Assert.Contains(leftover.Notices, notice => notice.Contains("40", StringComparison.Ordinal));

        RecordedDocument split = RecordingCompiler.Compile(
            [RecordedEvent.Wheel(1, 150 * 120)],
            "Wheel",
            "rec-wheel");
        Assert.Equal([100, 50], split.Document!.Actions.Cast<MouseWheelAction>().Select(action => action.Notches).ToArray());
    }

    [Fact]
    public void Modifiers_can_be_left_out_and_an_unmatched_release_is_skipped()
    {
        RecordedDocument recorded = RecordingCompiler.Compile(
            [
                RecordedEvent.KeyPress(1, InputKey.Shift, true),
                RecordedEvent.KeyPress(2, InputKey.A, true),
                RecordedEvent.KeyPress(3, InputKey.Shift, false),
                RecordedEvent.KeyPress(4, InputKey.A, false),
                RecordedEvent.Mouse(5, MouseButton.Right, false),
            ],
            "Keys",
            "rec-keys",
            includeModifiers: false);

        Assert.True(recorded.IsReady);
        Assert.Equal(2, recorded.Document!.Actions.OfType<KeyboardKeyAction>().Count());
        Assert.DoesNotContain(recorded.Document.Actions, action => action is KeyboardKeyAction key && key.Key == "Shift");
        Assert.Contains(recorded.Notices, notice => notice.Contains("button release", StringComparison.Ordinal));
    }

    [Fact]
    public void A_move_outside_the_schema_range_is_left_out()
    {
        RecordedDocument recorded = RecordingCompiler.Compile(
            [RecordedEvent.Move(1, 40_000, 1), RecordedEvent.Move(2, 3, 4)],
            "Range",
            "rec-range");

        Assert.True(recorded.IsReady);
        var move = Assert.IsType<MouseMoveAction>(Assert.Single(recorded.Document!.Actions));
        Assert.Equal(3, move.X);
        Assert.Contains(recorded.Notices, notice => notice.Contains("1 move", StringComparison.Ordinal));
    }

    [Fact]
    public void An_invalid_name_does_not_produce_a_document()
    {
        RecordedDocument recorded = RecordingCompiler.Compile([], "  spaced", "rec-1");

        Assert.False(recorded.IsReady);
        Assert.Contains(recorded.Notices, notice => notice.Contains("Name must not start or end with whitespace.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_document_replays_through_the_runner()
    {
        long down = MonotonicClock.TicksFromMilliseconds(50);
        long up = MonotonicClock.TicksFromMilliseconds(70);
        RecordedDocument recorded = RecordingCompiler.Compile(
            [
                RecordedEvent.Move(0, 10, 20),
                RecordedEvent.Mouse(down, MouseButton.Left, true),
                RecordedEvent.Mouse(up, MouseButton.Left, false),
                RecordedEvent.Wheel(up, 120),
            ],
            "Replay",
            "rec-replay");
        Assert.True(recorded.IsReady, recorded.Notices.FirstOrDefault());

        var native = new CapturingNative();
        var waits = new List<int>();
        using var pause = new MacroPause();
        var manager = new InputStateManager(new SendInputBatch(native));
        var executor = new MacroInputExecutor(
            manager,
            (milliseconds, _) =>
            {
                waits.Add(milliseconds);
                return new MacroWaitSlice(milliseconds, false);
            },
            () => new VirtualScreen(0, 0, 1920, 1080),
            pause);
        MacroRunContract contract = MacroExecution.Compile(recorded.Document!);

        MacroRunResult result = MacroRunner.Run(contract, executor);

        Assert.Equal(MacroRunStatus.Completed, result.Status);
        Assert.Equal([RecordingTime.ElapsedMilliseconds(0, down)], waits);
        Assert.Equal(3, native.Calls);
        Assert.Equal(120u, native.Packets[^1].Data.Mouse.mouseData);
        Assert.Empty(manager.Snapshot());
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<NativeInput> Packets { get; } = [];

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            Packets.AddRange(inputs);
            return ((uint)inputs.Length, 0);
        }
    }
}
