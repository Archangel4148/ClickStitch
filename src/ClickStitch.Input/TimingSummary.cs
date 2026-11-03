namespace ClickStitch.Input;

/// <summary>
/// Nearest-rank percentiles. For a sorted sample of size n, rank p uses index <c>ceil(p/100 * n) - 1</c>.
/// </summary>
internal readonly struct TimingSummary
{
    internal TimingSummary(int count, long minimum, long meanTicks, long p50, long p95, long p99, long maximum)
    {
        Count = count;
        Minimum = minimum;
        MeanTicks = meanTicks;
        P50 = p50;
        P95 = p95;
        P99 = p99;
        Maximum = maximum;
    }

    internal int Count { get; }

    internal long Minimum { get; }

    internal long MeanTicks { get; }

    internal long P50 { get; }

    internal long P95 { get; }

    internal long P99 { get; }

    internal long Maximum { get; }

    internal static TimingSummary Summarize(ReadOnlySpan<long> values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one sample is required.", nameof(values));
        }

        long[] sorted = values.ToArray();
        Array.Sort(sorted);
        long sum = 0;
        for (int index = 0; index < sorted.Length; index++)
        {
            sum += sorted[index];
        }

        long mean = sum / sorted.Length;
        return new TimingSummary(
            sorted.Length,
            sorted[0],
            mean,
            Percentile(sorted, 50),
            Percentile(sorted, 95),
            Percentile(sorted, 99),
            sorted[^1]);
    }

    private static long Percentile(long[] sorted, int percent)
    {
        int index = (int)(((long)sorted.Length * percent + 99) / 100 - 1);
        if (index < 0)
        {
            index = 0;
        }

        if (index >= sorted.Length)
        {
            index = sorted.Length - 1;
        }

        return sorted[index];
    }
}
