namespace ClickStitch.Core;

/// <summary>
/// One validation failure. <see cref="Path"/> names the field, such as <c>actions[0].button</c>.
/// </summary>
public sealed class MacroIssue
{
    internal MacroIssue(string path, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Path = path;
        Message = message;
    }

    public string Path { get; }

    public string Message { get; }
}
