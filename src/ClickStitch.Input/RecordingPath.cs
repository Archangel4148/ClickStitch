namespace ClickStitch.Input;

/// <summary>
/// How a finished recording thins movement. Zero leaves that step out.
/// The hook does not read these values.
/// </summary>
internal readonly record struct MovementOptimization
{
    internal const int MaximumSampleMilliseconds = 60_000;
    internal const int MaximumPixels = 10_000;

    internal int SampleMilliseconds { get; init; }

    internal int MinimumDistance { get; init; }

    internal int Tolerance { get; init; }

    internal static MovementOptimization From(RecordingOptions options) => new()
    {
        SampleMilliseconds = options.SampleMilliseconds,
        MinimumDistance = options.MinimumDistance,
        Tolerance = options.Tolerance,
    };

    internal void Validate()
    {
        if (SampleMilliseconds < 0 || SampleMilliseconds > MaximumSampleMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SampleMilliseconds),
                SampleMilliseconds,
                "Sample interval must be from 0 through 60,000 milliseconds.");
        }

        if (MinimumDistance < 0 || MinimumDistance > MaximumPixels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumDistance),
                MinimumDistance,
                "Minimum distance must be from 0 through 10,000 pixels.");
        }

        if (Tolerance < 0 || Tolerance > MaximumPixels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Tolerance),
                Tolerance,
                "Tolerance must be from 0 through 10,000 pixels.");
        }
    }
}

/// <summary>
/// Builds a shorter copy of a finished recording. Buttons, keys, and the wheel stay where they are
/// and split the movement around them. The source list is not changed.
/// </summary>
internal static class RecordingPath
{
    internal static List<RecordedEvent> Build(IReadOnlyList<RecordedEvent> source, MovementOptimization optimization)
    {
        ArgumentNullException.ThrowIfNull(source);
        optimization.Validate();
        var result = new List<RecordedEvent>(source.Count);
        int index = 0;
        while (index < source.Count)
        {
            if (source[index].Kind != RecordedKind.Move)
            {
                result.Add(source[index]);
                index++;
                continue;
            }

            int end = index + 1;
            while (end < source.Count && source[end].Kind == RecordedKind.Move)
            {
                end++;
            }

            AppendRun(source, index, end, optimization, result);
            index = end;
        }

        return result;
    }

    private static void AppendRun(
        IReadOnlyList<RecordedEvent> source,
        int start,
        int end,
        MovementOptimization optimization,
        List<RecordedEvent> result)
    {
        var run = new List<RecordedEvent> { source[start] };
        if (end - start > 1)
        {
            RecordedEvent last = source[start];
            for (int i = start + 1; i < end - 1; i++)
            {
                RecordedEvent point = source[i];
                if (TooSoon(optimization, last, point) || TooClose(optimization, last, point))
                {
                    continue;
                }

                run.Add(point);
                last = point;
            }

            RecordedEvent tail = source[end - 1];
            if (tail.Timestamp != last.Timestamp || tail.X != last.X || tail.Y != last.Y)
            {
                run.Add(tail);
            }
        }

        if (optimization.Tolerance > 0 && run.Count > 2)
        {
            run = Reduce(run, optimization.Tolerance);
        }

        result.AddRange(run);
    }

    private static bool TooSoon(MovementOptimization optimization, RecordedEvent last, RecordedEvent point) =>
        optimization.SampleMilliseconds > 0 &&
        RecordingTime.ElapsedMilliseconds(last.Timestamp, point.Timestamp) < optimization.SampleMilliseconds;

    private static bool TooClose(MovementOptimization optimization, RecordedEvent last, RecordedEvent point) =>
        optimization.MinimumDistance > 0 &&
        DistanceSquared(last, point) < (long)optimization.MinimumDistance * optimization.MinimumDistance;

    private static List<RecordedEvent> Reduce(List<RecordedEvent> run, int tolerance)
    {
        int count = run.Count;
        var keep = new bool[count];
        keep[0] = true;
        keep[count - 1] = true;
        var pending = new Stack<(int Start, int End)>();
        pending.Push((0, count - 1));
        while (pending.Count > 0)
        {
            (int start, int end) = pending.Pop();
            double farthest = 0;
            int index = -1;
            for (int i = start + 1; i < end; i++)
            {
                double distance = Perpendicular(run[start], run[end], run[i]);
                if (distance > farthest)
                {
                    farthest = distance;
                    index = i;
                }
            }

            if (index >= 0 && farthest > tolerance)
            {
                keep[index] = true;
                pending.Push((start, index));
                pending.Push((index, end));
            }
        }

        var reduced = new List<RecordedEvent>(count);
        for (int i = 0; i < count; i++)
        {
            if (keep[i])
            {
                reduced.Add(run[i]);
            }
        }

        return reduced;
    }

    private static double Perpendicular(RecordedEvent start, RecordedEvent end, RecordedEvent point)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        double offsetX = point.X - start.X;
        double offsetY = point.Y - start.Y;
        if (lengthSquared == 0)
        {
            return Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
        }

        double cross = (offsetX * dy) - (offsetY * dx);
        return Math.Abs(cross) / Math.Sqrt(lengthSquared);
    }

    private static long DistanceSquared(RecordedEvent start, RecordedEvent end)
    {
        long dx = (long)end.X - start.X;
        long dy = (long)end.Y - start.Y;
        return (dx * dx) + (dy * dy);
    }
}
