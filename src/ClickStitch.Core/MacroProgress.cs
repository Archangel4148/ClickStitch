namespace ClickStitch.Core;

public enum MacroProgressKind
{
    ActionStarted,
    ActionCompleted,
    Paused,
    Resumed,
    ActionFailed,
    Finished,
}

/// <summary>
/// One progress record. The runner does not wait for a reader.
/// <see cref="ActionId"/> is the macro id for <see cref="MacroProgressKind.Finished"/>.
/// </summary>
public readonly record struct MacroProgressEvent(MacroProgressKind Kind, string ActionId, string? Message);
