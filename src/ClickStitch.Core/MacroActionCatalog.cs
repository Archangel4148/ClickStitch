namespace ClickStitch.Core;

/// <summary>
/// One field a format-1 action accepts. This describes the field. <see cref="MacroJson"/> already enforces it.
/// </summary>
public sealed class MacroParameterDescriptor
{
    internal MacroParameterDescriptor(string name, bool required, string summary)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(summary);
        Name = name;
        Required = required;
        Summary = summary;
    }

    public string Name { get; }

    public bool Required { get; }

    public string Summary { get; }
}

/// <summary>
/// The stable description of one built-in action type. Plugin actions are not in this catalog.
/// </summary>
public sealed class MacroActionDescriptor
{
    internal MacroActionDescriptor(string typeId, string displayName, string description, IReadOnlyList<MacroParameterDescriptor> parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeId);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(parameters);
        TypeId = typeId;
        DisplayName = displayName;
        Description = description;
        Parameters = parameters;
    }

    /// <summary>The <c>type</c> string stored in a document, such as <c>mouse.click</c>.</summary>
    public string TypeId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public IReadOnlyList<MacroParameterDescriptor> Parameters { get; }
}

/// <summary>
/// The eight format-1 actions. <c>repeat.until</c> and <c>branch.if</c> are not included.
/// </summary>
public static class MacroActionCatalog
{
    private static readonly IReadOnlyList<MacroParameterDescriptor> Common =
    [
        new("id", true, "Unique in the document. Letters, digits, '.', '_', and '-'."),
        new("comment", false, "Note for the author. It is not executed."),
    ];

    private static readonly Dictionary<string, MacroActionDescriptor> ByType = Build();

    public static IReadOnlyList<MacroParameterDescriptor> CommonParameters { get; } = Common;

    public static IReadOnlyList<MacroActionDescriptor> BuiltIn { get; } = ByType.Values.ToArray();

    public static bool TryGet(string typeId, out MacroActionDescriptor descriptor)
    {
        if (typeId is null)
        {
            descriptor = null!;
            return false;
        }

        return ByType.TryGetValue(typeId, out descriptor!);
    }

    private static Dictionary<string, MacroActionDescriptor> Build()
    {
        MacroActionDescriptor[] descriptors =
        [
            Describe(
                "mouse.click",
                "Click",
                "Presses a mouse button and releases it before the action finishes.",
                [
                    new("button", true, "left, right, or middle."),
                    new("coordinateMode", false, "virtualDesktop, primaryDisplay, relative, or cursor. Default virtualDesktop."),
                    new("x", false, "Required with y unless the mode is cursor."),
                    new("y", false, "Required with x unless the mode is cursor."),
                    new("clickCount", false, "1, 2, or 3. Default 1."),
                ]),
            Describe(
                "mouse.move",
                "Move pointer",
                "Moves the pointer. Cursor mode is not allowed.",
                [
                    new("coordinateMode", false, "virtualDesktop, primaryDisplay, or relative. Default virtualDesktop."),
                    new("x", true, "Integer from -32768 to 32767."),
                    new("y", true, "Integer from -32768 to 32767."),
                ]),
            Describe(
                "mouse.hold",
                "Hold or release",
                "Changes one mouse button. A point, when present, is part of that same change.",
                [
                    new("button", true, "left, right, or middle."),
                    new("state", true, "down or up."),
                    new("coordinateMode", false, "Used when x and y are present. Cursor omits them."),
                    new("x", false, "Present only together with y."),
                    new("y", false, "Present only together with x."),
                ]),
            Describe(
                "mouse.wheel",
                "Scroll wheel",
                "Scrolls by notches. Positive is away from the user.",
                [
                    new("notches", true, "Non-zero integer from -100 to 100."),
                ]),
            Describe(
                "keyboard.key",
                "Key",
                "Presses or releases one schema key. Modifiers are their own actions.",
                [
                    new("key", true, "A format-1 key name, such as A or Shift."),
                    new("state", true, "down or up."),
                ]),
            Describe(
                "wait",
                "Wait",
                "Waits for a number of milliseconds. Zero finishes immediately.",
                [
                    new("durationMs", true, "Integer from 0 to 3600000."),
                ]),
            Describe(
                "repeat.count",
                "Repeat",
                "Runs the nested actions a fixed number of times.",
                [
                    new("count", true, "Integer from 1 to 1000000."),
                    new("actions", true, "Nested actions. The list may be empty."),
                ]),
            Describe(
                "comment",
                "Comment",
                "Text for the author. The runner submits nothing.",
                [
                    new("text", true, "String from 0 to 2000 characters."),
                ]),
        ];

        return descriptors.ToDictionary(static item => item.TypeId, StringComparer.Ordinal);
    }

    private static MacroActionDescriptor Describe(string typeId, string displayName, string description, MacroParameterDescriptor[] parameters) =>
        new(typeId, displayName, description, parameters);
}
