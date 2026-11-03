namespace ClickStitch.Core;

/// <summary>
/// Where a stop request is honored. The runner is not part of this type.
/// </summary>
public enum MacroStopBehavior
{
    /// <summary>Before the action starts, or after its one submission. Not between the parts of that submission.</summary>
    AroundSubmission,

    /// <summary>A wait ends. Pause keeps whatever time is left.</summary>
    EndWait,

    /// <summary>Before the next child and before the next iteration. The count is not expanded here.</summary>
    BetweenRepeatSteps,

    /// <summary>Nothing is submitted.</summary>
    Skip,
}

/// <summary>
/// What pause does to one action.
/// </summary>
public enum MacroPauseBehavior
{
    /// <summary>Pause waits until this action has finished, then holds the next one.</summary>
    BeforeNextAction,

    /// <summary>The rest of a wait is kept. Held buttons and keys stay down.</summary>
    KeepRemainingWait,
}

/// <summary>
/// A point the runner will submit. <see cref="MacroCoordinateMode.Cursor"/> is never stored here.
/// </summary>
public readonly record struct MacroPoint(MacroCoordinateMode Mode, int X, int Y);

/// <summary>
/// What the runner must do for one saved action. Building this does not submit input.
/// </summary>
public abstract class MacroActionContract
{
    private protected MacroActionContract(string actionId, string typeId, MacroStopBehavior stop, MacroPauseBehavior pause)
    {
        ActionId = actionId;
        TypeId = typeId;
        Stop = stop;
        Pause = pause;
    }

    public string ActionId { get; }

    public string TypeId { get; }

    public MacroStopBehavior Stop { get; }

    public MacroPauseBehavior Pause { get; }
}

public sealed class ClickContract : MacroActionContract
{
    internal ClickContract(string actionId, MacroMouseButton button, int clickCount, MacroPoint? point)
        : base(actionId, "mouse.click", MacroStopBehavior.AroundSubmission, MacroPauseBehavior.BeforeNextAction)
    {
        Button = button;
        ClickCount = clickCount;
        Point = point;
    }

    public MacroMouseButton Button { get; }

    public int ClickCount { get; }

    /// <summary>Null when the click uses the current cursor. Otherwise the move and the click are one submission.</summary>
    public MacroPoint? Point { get; }
}

public sealed class MoveContract : MacroActionContract
{
    internal MoveContract(string actionId, MacroPoint point)
        : base(actionId, "mouse.move", MacroStopBehavior.AroundSubmission, MacroPauseBehavior.BeforeNextAction) =>
        Point = point;

    public MacroPoint Point { get; }
}

public sealed class HoldContract : MacroActionContract
{
    internal HoldContract(string actionId, MacroMouseButton button, MacroPressState state, MacroPoint? point)
        : base(actionId, "mouse.hold", MacroStopBehavior.AroundSubmission, MacroPauseBehavior.BeforeNextAction)
    {
        Button = button;
        State = state;
        Point = point;
    }

    public MacroMouseButton Button { get; }

    public MacroPressState State { get; }

    /// <summary>Null when the button changes at the current cursor. A point is moved in the same submission as the button.</summary>
    public MacroPoint? Point { get; }
}

public sealed class WheelContract : MacroActionContract
{
    /// <summary>Windows reports one notch as this many wheel units.</summary>
    public const int WindowsWheelDelta = 120;

    internal WheelContract(string actionId, int notches)
        : base(actionId, "mouse.wheel", MacroStopBehavior.AroundSubmission, MacroPauseBehavior.BeforeNextAction) =>
        Notches = notches;

    public int Notches { get; }
}

public sealed class KeyContract : MacroActionContract
{
    internal KeyContract(string actionId, string key, MacroPressState state)
        : base(actionId, "keyboard.key", MacroStopBehavior.AroundSubmission, MacroPauseBehavior.BeforeNextAction)
    {
        Key = key;
        State = state;
    }

    public string Key { get; }

    public MacroPressState State { get; }
}

public sealed class WaitContract : MacroActionContract
{
    internal WaitContract(string actionId, int durationMs)
        : base(actionId, "wait", MacroStopBehavior.EndWait, MacroPauseBehavior.KeepRemainingWait) =>
        DurationMs = durationMs;

    public int DurationMs { get; }
}

public sealed class RepeatContract : MacroActionContract
{
    internal RepeatContract(string actionId, int count, IReadOnlyList<MacroActionContract> actions)
        : base(actionId, "repeat.count", MacroStopBehavior.BetweenRepeatSteps, MacroPauseBehavior.BeforeNextAction)
    {
        Count = count;
        Actions = actions;
    }

    public int Count { get; }

    public IReadOnlyList<MacroActionContract> Actions { get; }
}

public sealed class CommentContract : MacroActionContract
{
    internal CommentContract(string actionId, string text)
        : base(actionId, "comment", MacroStopBehavior.Skip, MacroPauseBehavior.BeforeNextAction) =>
        Text = text;

    public string Text { get; }
}

/// <summary>
/// The runnable shape of one validated document. Variables and unknown members are not copied.
/// </summary>
public sealed class MacroRunContract
{
    internal MacroRunContract(string macroId, bool stopOnError, IReadOnlyList<MacroActionContract> actions)
    {
        MacroId = macroId;
        StopOnError = stopOnError;
        Actions = actions;
    }

    public string MacroId { get; }

    /// <summary>When true, a submission Windows does not fully accept ends the run.</summary>
    public bool StopOnError { get; }

    public IReadOnlyList<MacroActionContract> Actions { get; }
}

/// <summary>
/// Builds the execution contract for a document <see cref="MacroJson"/> has already accepted.
/// </summary>
public static class MacroExecution
{
    public static MacroRunContract Compile(MacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new MacroRunContract(document.Id, document.StopOnError, Compile(document.Actions));
    }

    private static IReadOnlyList<MacroActionContract> Compile(IReadOnlyList<MacroAction> actions)
    {
        var compiled = new MacroActionContract[actions.Count];
        for (int index = 0; index < actions.Count; index++)
        {
            compiled[index] = Compile(actions[index]);
        }

        return compiled;
    }

    private static MacroActionContract Compile(MacroAction action) => action switch
    {
        MouseClickAction click => new ClickContract(
            click.Id,
            click.Button,
            click.ClickCount,
            click.Mode == MacroCoordinateMode.Cursor ? null : new MacroPoint(click.Mode, click.X!.Value, click.Y!.Value)),
        MouseMoveAction move => new MoveContract(move.Id, new MacroPoint(move.Mode, move.X, move.Y)),
        MouseHoldAction hold => new HoldContract(
            hold.Id,
            hold.Button,
            hold.State,
            hold.X is int x && hold.Y is int y
                ? new MacroPoint(hold.Mode ?? MacroCoordinateMode.VirtualDesktop, x, y)
                : null),
        MouseWheelAction wheel => new WheelContract(wheel.Id, wheel.Notches),
        KeyboardKeyAction key => new KeyContract(key.Id, key.Key, key.State),
        WaitAction wait => new WaitContract(wait.Id, wait.DurationMs),
        RepeatCountAction repeat => new RepeatContract(repeat.Id, repeat.Count, Compile(repeat.Actions)),
        CommentAction comment => new CommentContract(comment.Id, comment.Text),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
