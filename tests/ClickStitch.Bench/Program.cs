using ClickStitch.Input;

MachineFacts facts = MachineFacts.Capture();
IReadOnlyList<TimingRun> runs;
try
{
    runs = TimingSuite.Run(Console.WriteLine);
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

string report = TimingBenchmark.FormatReport(facts, runs);
Console.WriteLine(report);
File.WriteAllText(Path.Combine(Path.GetTempPath(), "ClickStitch-timing-benchmark.txt"), report);

try
{
    TimingSuite.EnsureMeasured(runs);
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

return 0;
