using System.Text.Json.Nodes;

namespace ClickStitch.Core;

public enum MacroMouseButton
{
    Left,
    Right,
    Middle,
}

public enum MacroPressState
{
    Down,
    Up,
}

public enum MacroCoordinateMode
{
    VirtualDesktop,
    PrimaryDisplay,
    Relative,
    Cursor,
}

public enum MacroVariableType
{
    Number,
    Text,
    Boolean,
}

/// <summary>
/// A JSON member this format does not interpret. Execution ignores it. Writing the document puts it back.
/// </summary>
public sealed class MacroExtra
{
    internal MacroExtra(string name, JsonNode value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public JsonNode Value { get; }
}

public sealed class MacroMetadata
{
    internal MacroMetadata(string? createdUtc, string? modifiedUtc, List<MacroExtra> extras)
    {
        CreatedUtc = createdUtc;
        ModifiedUtc = modifiedUtc;
        Extras = extras;
    }

    public string? CreatedUtc { get; }

    public string? ModifiedUtc { get; }

    public IReadOnlyList<MacroExtra> Extras { get; }
}

public sealed class MacroCoordinateSpace
{
    internal MacroCoordinateSpace(List<MacroExtra> extras) => Extras = extras;

    public string System { get; } = "virtualDesktopPixels";

    public IReadOnlyList<MacroExtra> Extras { get; }
}

public sealed class MacroSettings
{
    internal MacroSettings(bool stopOnError, bool stopOnErrorSpecified, List<MacroExtra> extras)
    {
        StopOnError = stopOnError;
        StopOnErrorSpecified = stopOnErrorSpecified;
        Extras = extras;
    }

    public bool StopOnError { get; }

    internal bool StopOnErrorSpecified { get; }

    public IReadOnlyList<MacroExtra> Extras { get; }
}

public sealed class MacroVariable
{
    internal MacroVariable(string name, MacroVariableType type, JsonNode defaultValue, List<MacroExtra> extras)
    {
        Name = name;
        Type = type;
        Default = defaultValue;
        Extras = extras;
    }

    public string Name { get; }

    public MacroVariableType Type { get; }

    public JsonNode Default { get; }

    public IReadOnlyList<MacroExtra> Extras { get; }
}

/// <summary>
/// One validated format-1 macro. It describes actions. It does not run them.
/// </summary>
public sealed class MacroDocument
{
    internal MacroDocument(
        string id,
        string name,
        string description,
        bool descriptionSpecified,
        MacroMetadata? metadata,
        MacroCoordinateSpace? coordinateSpace,
        MacroSettings? settings,
        IReadOnlyList<MacroVariable> variables,
        bool variablesSpecified,
        bool pluginDependenciesSpecified,
        IReadOnlyList<MacroAction> actions,
        List<MacroExtra> extras)
    {
        Id = id;
        Name = name;
        Description = description;
        DescriptionSpecified = descriptionSpecified;
        Metadata = metadata;
        CoordinateSpace = coordinateSpace;
        Settings = settings;
        Variables = variables;
        VariablesSpecified = variablesSpecified;
        PluginDependenciesSpecified = pluginDependenciesSpecified;
        Actions = actions;
        Extras = extras;
    }

    public int FormatVersion => MacroFormat.CurrentVersion;

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    internal bool DescriptionSpecified { get; }

    public MacroMetadata? Metadata { get; }

    public MacroCoordinateSpace? CoordinateSpace { get; }

    public MacroSettings? Settings { get; }

    /// <summary>True when <see cref="Settings"/> omits the flag or sets it true.</summary>
    public bool StopOnError => Settings is null || !Settings.StopOnErrorSpecified || Settings.StopOnError;

    public IReadOnlyList<MacroVariable> Variables { get; }

    internal bool VariablesSpecified { get; }

    internal bool PluginDependenciesSpecified { get; }

    public IReadOnlyList<MacroAction> Actions { get; }

    public IReadOnlyList<MacroExtra> Extras { get; }
}

public abstract class MacroAction
{
    private protected MacroAction(string id, string? comment, List<MacroExtra> extras)
    {
        Id = id;
        Comment = comment;
        Extras = extras;
    }

    public string Id { get; }

    public string? Comment { get; }

    public IReadOnlyList<MacroExtra> Extras { get; }
}

public sealed class MouseClickAction : MacroAction
{
    internal MouseClickAction(
        string id,
        string? comment,
        List<MacroExtra> extras,
        MacroMouseButton button,
        MacroCoordinateMode mode,
        bool modeSpecified,
        int? x,
        int? y,
        int clickCount,
        bool clickCountSpecified)
        : base(id, comment, extras)
    {
        Button = button;
        Mode = mode;
        ModeSpecified = modeSpecified;
        X = x;
        Y = y;
        ClickCount = clickCount;
        ClickCountSpecified = clickCountSpecified;
    }

    public MacroMouseButton Button { get; }

    public MacroCoordinateMode Mode { get; }

    internal bool ModeSpecified { get; }

    public int? X { get; }

    public int? Y { get; }

    public int ClickCount { get; }

    internal bool ClickCountSpecified { get; }
}

public sealed class MouseMoveAction : MacroAction
{
    internal MouseMoveAction(
        string id,
        string? comment,
        List<MacroExtra> extras,
        MacroCoordinateMode mode,
        bool modeSpecified,
        int x,
        int y)
        : base(id, comment, extras)
    {
        Mode = mode;
        ModeSpecified = modeSpecified;
        X = x;
        Y = y;
    }

    public MacroCoordinateMode Mode { get; }

    internal bool ModeSpecified { get; }

    public int X { get; }

    public int Y { get; }
}

public sealed class MouseHoldAction : MacroAction
{
    internal MouseHoldAction(
        string id,
        string? comment,
        List<MacroExtra> extras,
        MacroMouseButton button,
        MacroPressState state,
        MacroCoordinateMode? mode,
        bool modeSpecified,
        int? x,
        int? y)
        : base(id, comment, extras)
    {
        Button = button;
        State = state;
        Mode = mode;
        ModeSpecified = modeSpecified;
        X = x;
        Y = y;
    }

    public MacroMouseButton Button { get; }

    public MacroPressState State { get; }

    /// <summary>
    /// Null when the button changes at the current cursor and the mode was omitted.
    /// <see cref="MacroCoordinateMode.VirtualDesktop"/> when a point was given and the mode was omitted.
    /// </summary>
    public MacroCoordinateMode? Mode { get; }

    internal bool ModeSpecified { get; }

    public int? X { get; }

    public int? Y { get; }
}

public sealed class MouseWheelAction : MacroAction
{
    internal MouseWheelAction(string id, string? comment, List<MacroExtra> extras, int notches)
        : base(id, comment, extras) => Notches = notches;

    public int Notches { get; }
}

public sealed class KeyboardKeyAction : MacroAction
{
    internal KeyboardKeyAction(string id, string? comment, List<MacroExtra> extras, string key, MacroPressState state)
        : base(id, comment, extras)
    {
        Key = key;
        State = state;
    }

    public string Key { get; }

    public MacroPressState State { get; }
}

public sealed class WaitAction : MacroAction
{
    internal WaitAction(string id, string? comment, List<MacroExtra> extras, int durationMs)
        : base(id, comment, extras) => DurationMs = durationMs;

    public int DurationMs { get; }
}

public sealed class RepeatCountAction : MacroAction
{
    internal RepeatCountAction(string id, string? comment, List<MacroExtra> extras, int count, IReadOnlyList<MacroAction> actions)
        : base(id, comment, extras)
    {
        Count = count;
        Actions = actions;
    }

    public int Count { get; }

    public IReadOnlyList<MacroAction> Actions { get; }
}

public sealed class CommentAction : MacroAction
{
    internal CommentAction(string id, string? comment, List<MacroExtra> extras, string text)
        : base(id, comment, extras) => Text = text;

    public string Text { get; }
}
