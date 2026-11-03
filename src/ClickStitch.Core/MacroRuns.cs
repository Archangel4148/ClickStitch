namespace ClickStitch.Core;

/// <summary>
/// A document that has either been accepted for execution or rejected with validation issues.
/// An invalid document has no contract, so a run cannot start from it.
/// </summary>
public sealed class PreparedMacro
{
    internal PreparedMacro(MacroRunContract? contract, IReadOnlyList<MacroIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Contract = contract;
        Issues = issues;
    }

    public MacroRunContract? Contract { get; }

    public IReadOnlyList<MacroIssue> Issues { get; }

    public bool IsReady => Contract is not null;
}

/// <summary>
/// Validates a format-1 document before any action is run.
/// </summary>
public static class MacroRuns
{
    public static PreparedMacro Prepare(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Prepare(MacroJson.Read(json));
    }

    public static PreparedMacro Prepare(ReadOnlySpan<byte> utf8) => Prepare(MacroJson.Read(utf8));

    private static PreparedMacro Prepare(MacroReadResult read)
    {
        if (!read.IsValid || read.Document is null)
        {
            return new PreparedMacro(null, read.Issues);
        }

        return new PreparedMacro(MacroExecution.Compile(read.Document), read.Issues);
    }
}
