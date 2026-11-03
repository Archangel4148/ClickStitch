namespace ClickStitch.Performance.Tests;

/// <summary>
/// Keeps the performance assembly in the default run without submitting input.
/// The desktop benchmark is opt-in. Run it with <c>make bench</c>, which prints progress as it goes.
/// </summary>
public sealed class BenchmarkOptInTests
{
    [Fact]
    public void The_desktop_benchmark_is_not_part_of_the_default_run()
    {
        Assert.Equal(WaitStrategy.Hybrid, DeadlineWait.ProductStrategy);
    }
}

/// <summary>
/// Same measurement as <c>make bench</c>. <c>dotnet test</c> may hold these progress lines until the test ends.
/// </summary>
[Trait("Category", "Performance")]
public sealed class TimingBenchmarkTests
{
    [Fact(Timeout = 600_000)]
    public void Scheduled_pointer_moves_at_one_ten_and_fifty_milliseconds()
    {
        MachineFacts facts = MachineFacts.Capture();
        IReadOnlyList<TimingRun> runs = TimingSuite.Run(WriteLive);
        string report = TimingBenchmark.FormatReport(facts, runs);
        Console.WriteLine(report);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "ClickStitch-timing-benchmark.txt"), report);
        TimingSuite.EnsureMeasured(runs);
    }

    private static void WriteLive(string message)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message + Environment.NewLine);
        Stream stdout = Console.OpenStandardOutput();
        stdout.Write(bytes);
        stdout.Flush();
    }
}
