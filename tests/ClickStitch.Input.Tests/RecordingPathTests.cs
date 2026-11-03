namespace ClickStitch.Input.Tests;

public sealed class RecordingPathTests
{
    [Fact]
    public void Zero_settings_keep_every_sample()
    {
        RecordedEvent[] source =
        [
            Move(0, 0, 1),
            Move(10, 0, 2),
            Move(20, 0, 3),
            RecordedEvent.Mouse(4, MouseButton.Left, true),
            RecordedEvent.Wheel(5, 120),
        ];

        List<RecordedEvent> copy = RecordingPath.Build(source, default);

        Assert.Equal(source, copy);
        Assert.NotSame(source, copy);
    }

    [Fact]
    public void Sampling_drops_moves_inside_the_interval_and_keeps_the_end()
    {
        long soon = MonotonicClock.TicksFromMilliseconds(40);
        long later = MonotonicClock.TicksFromMilliseconds(80);
        long end = MonotonicClock.TicksFromMilliseconds(90);
        Assert.True(RecordingTime.ElapsedMilliseconds(0, soon) < 100);
        Assert.True(RecordingTime.ElapsedMilliseconds(0, later) < 100);
        Assert.True(RecordingTime.ElapsedMilliseconds(0, end) < 100);

        List<RecordedEvent> copy = RecordingPath.Build(
            [Move(0, 0, 0), Move(1, 0, soon), Move(2, 0, later), Move(3, 0, end)],
            new MovementOptimization { SampleMilliseconds = 100 });

        Assert.Equal([0, 3], copy.Select(item => item.X).ToArray());
    }

    [Fact]
    public void A_move_at_the_sample_interval_is_kept()
    {
        long onInterval = MonotonicClock.TicksFromMilliseconds(100);
        long end = MonotonicClock.TicksFromMilliseconds(250);
        Assert.True(RecordingTime.ElapsedMilliseconds(0, onInterval) >= 100);

        List<RecordedEvent> copy = RecordingPath.Build(
            [Move(0, 0, 0), Move(10, 0, onInterval), Move(20, 0, end)],
            new MovementOptimization { SampleMilliseconds = 100 });

        Assert.Equal([0, 10, 20], copy.Select(item => item.X).ToArray());
    }

    [Fact]
    public void Minimum_distance_drops_a_short_step_and_keeps_the_end()
    {
        List<RecordedEvent> copy = RecordingPath.Build(
            [Move(0, 0, 1), Move(3, 4, 2), Move(10, 0, 3), Move(12, 0, 4)],
            new MovementOptimization { MinimumDistance = 10 });

        Assert.Equal([(0, 0), (10, 0), (12, 0)], copy.Select(item => (item.X, item.Y)).ToArray());
    }

    [Fact]
    public void Tolerance_drops_a_point_on_the_line_and_keeps_a_corner()
    {
        List<RecordedEvent> line = RecordingPath.Build(
            [Move(0, 0, 1), Move(10, 0, 2), Move(20, 0, 3)],
            new MovementOptimization { Tolerance = 1 });
        Assert.Equal([0, 20], line.Select(item => item.X).ToArray());

        List<RecordedEvent> corner = RecordingPath.Build(
            [Move(0, 0, 1), Move(10, 0, 2), Move(10, 10, 3)],
            new MovementOptimization { Tolerance = 5 });
        Assert.Equal([(0, 0), (10, 0), (10, 10)], corner.Select(item => (item.X, item.Y)).ToArray());

        List<RecordedEvent> softened = RecordingPath.Build(
            [Move(0, 0, 1), Move(10, 0, 2), Move(10, 10, 3)],
            new MovementOptimization { Tolerance = 8 });
        Assert.Equal([(0, 0), (10, 10)], softened.Select(item => (item.X, item.Y)).ToArray());
    }

    [Fact]
    public void A_point_exactly_on_the_tolerance_is_dropped()
    {
        List<RecordedEvent> copy = RecordingPath.Build(
            [Move(0, 0, 1), Move(10, 3, 2), Move(20, 0, 3)],
            new MovementOptimization { Tolerance = 3 });

        Assert.Equal([0, 20], copy.Select(item => item.X).ToArray());
    }

    [Fact]
    public void A_button_splits_the_path()
    {
        RecordedEvent[] source =
        [
            Move(0, 0, 1),
            Move(50, 0, 2),
            Move(100, 0, 3),
            RecordedEvent.Mouse(4, MouseButton.Left, true),
            Move(100, 0, 5),
            Move(150, 0, 6),
            Move(200, 0, 7),
        ];

        List<RecordedEvent> copy = RecordingPath.Build(source, new MovementOptimization { Tolerance = 1_000 });

        Assert.Equal(5, copy.Count);
        Assert.Equal(RecordedKind.Button, copy[2].Kind);
        Assert.Equal([(0, 0), (100, 0)], copy.Take(2).Select(item => (item.X, item.Y)).ToArray());
        Assert.Equal([(100, 0), (200, 0)], copy.Skip(3).Select(item => (item.X, item.Y)).ToArray());
    }

    [Fact]
    public void The_ranges_reject_values_outside_zero_through_their_maximum()
    {
        ArgumentOutOfRangeException sample = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecordingPath.Build([], new MovementOptimization { SampleMilliseconds = -1 }));
        Assert.Equal("SampleMilliseconds", sample.ParamName);
        Assert.StartsWith("Sample interval must be from 0 through 60,000 milliseconds.", sample.Message);

        ArgumentOutOfRangeException distance = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecordingPath.Build([], new MovementOptimization { MinimumDistance = 10_001 }));
        Assert.Equal("MinimumDistance", distance.ParamName);
        Assert.StartsWith("Minimum distance must be from 0 through 10,000 pixels.", distance.Message);

        ArgumentOutOfRangeException tolerance = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecordingPath.Build([], new MovementOptimization { Tolerance = -1 }));
        Assert.Equal("Tolerance", tolerance.ParamName);
        Assert.StartsWith("Tolerance must be from 0 through 10,000 pixels.", tolerance.Message);
    }

    private static RecordedEvent Move(int x, int y, long timestamp) => RecordedEvent.Move(timestamp, x, y);
}
