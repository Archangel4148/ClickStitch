using System.Globalization;
using System.Text.Json.Nodes;

namespace ClickStitch.Core;

/// <summary>
/// One choice shown for an editor field. <see cref="Value"/> is what the document stores.
/// </summary>
public sealed class EditorChoice
{
    internal EditorChoice(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }

    public string Label { get; }
}

/// <summary>
/// One labeled input on a list-editor row.
/// </summary>
public sealed class EditorField
{
    internal EditorField(string name, string label, string value, string help, IReadOnlyList<EditorChoice> choices)
    {
        Name = name;
        Label = label;
        Value = value;
        Help = help;
        Choices = choices;
    }

    public string Name { get; }

    public string Label { get; }

    public string Value { get; }

    public string Help { get; }

    public IReadOnlyList<EditorChoice> Choices { get; }
}

/// <summary>
/// One row in the list editor, including a nested action. <see cref="Path"/> is stable until the list changes.
/// </summary>
public sealed class EditorStep
{
    internal EditorStep(
        string path,
        string id,
        string typeId,
        string title,
        string category,
        string line,
        int depth,
        bool isRepeat,
        IReadOnlyList<EditorField> fields,
        IReadOnlyList<string> problems)
    {
        Path = path;
        Id = id;
        TypeId = typeId;
        Title = title;
        Category = category;
        Line = line;
        Depth = depth;
        IsRepeat = isRepeat;
        Fields = fields;
        Problems = problems;
    }

    /// <summary>Indexes from the root, separated by <c>/</c>. <c>0/1</c> is the second action inside the first.</summary>
    public string Path { get; }

    public string Id { get; }

    public string TypeId { get; }

    public string Title { get; }

    public string Category { get; }

    public string Line { get; }

    public int Depth { get; }

    public bool IsRepeat { get; }

    public IReadOnlyList<EditorField> Fields { get; }

    public IReadOnlyList<string> Problems { get; }
}

/// <summary>
/// Edits one format-1 macro as a list. It validates through <see cref="MacroJson"/> and does not run actions or submit input.
/// </summary>
public sealed class MacroEditor
{
    public const int UndoLimit = 50;

    private readonly List<Draft> _undo = [];
    private readonly List<Draft> _redo = [];
    private Draft _draft;
    private Draft _baseline;
    private MacroReadResult? _read;

    private MacroEditor(Draft draft)
    {
        _draft = draft;
        _baseline = draft.Clone();
    }

    public static MacroEditor Create()
    {
        return new MacroEditor(new Draft
        {
            Id = "m" + Guid.NewGuid().ToString("N"),
            Name = "Untitled macro",
        });
    }

    /// <summary>Opens a saved document. The result is unchanged until an edit.</summary>
    public static MacroEditor Open(MacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var draft = new Draft
        {
            Id = document.Id,
            Name = document.Name,
            Description = document.Description,
        };
        foreach (MacroAction action in document.Actions)
        {
            draft.Actions.Add(Stored(action));
        }

        draft.Next = NextNumber(draft.Actions);
        return new MacroEditor(draft);
    }

    public string Id => _draft.Id;

    public string Name => _draft.Name;

    public string Description => _draft.Description;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool HasUnsavedChanges => !Same(_draft, _baseline);

    public MacroDocument? Document => Read().Document;

    public string Preview
    {
        get
        {
            var lines = new List<string> { _draft.Name.Length == 0 ? "Untitled" : _draft.Name };
            IReadOnlyList<EditorStep> steps = Steps;
            if (steps.Count == 0)
            {
                lines.Add("This macro has no actions yet.");
            }
            else
            {
                foreach (EditorStep step in steps)
                {
                    lines.Add(new string(' ', step.Depth * 2) + step.Line);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public IReadOnlyList<string> DocumentProblems
    {
        get
        {
            var problems = new List<string>();
            foreach (MacroIssue issue in Read().Issues)
            {
                if (ActionPath(issue.Path) is null)
                {
                    problems.Add(issue.Message);
                }
            }

            return problems;
        }
    }

    public IReadOnlyList<EditorStep> Steps => Flatten(_draft.Actions, "", 0, Read().Issues);

    public void SetName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name == _draft.Name)
        {
            return;
        }

        Commit(() => _draft.Name = name);
    }

    public void SetDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description == _draft.Description)
        {
            return;
        }

        Commit(() => _draft.Description = description);
    }

    /// <summary>Appends a built-in action. <paramref name="parentPath"/> is a repeat, or null for the top list.</summary>
    public string Add(string typeId, string? parentPath)
    {
        if (!MacroActionCatalog.TryGet(typeId, out _))
        {
            throw new ArgumentException("That action is not a built-in format-1 action.", nameof(typeId));
        }

        if (parentPath is not null && !IsRepeat(parentPath))
        {
            throw new ArgumentException("Actions can only be added inside a repeat.", nameof(parentPath));
        }

        string created = "";
        Commit(() =>
        {
            List<DraftAction> list = _draft.Actions;
            if (parentPath is not null)
            {
                (List<DraftAction> parent, int parentIndex) = Locate(parentPath);
                list = parent[parentIndex].Children;
            }

            list.Add(CreateAction(typeId));
            created = parentPath is null
                ? (list.Count - 1).ToString(CultureInfo.InvariantCulture)
                : parentPath + "/" + (list.Count - 1).ToString(CultureInfo.InvariantCulture);
        });
        return created;
    }

    public void Remove(string path)
    {
        Locate(path);
        Commit(() =>
        {
            (List<DraftAction> list, int index) = Locate(path);
            list.RemoveAt(index);
        });
    }

    public string Duplicate(string path)
    {
        Locate(path);
        string created = "";
        Commit(() =>
        {
            (List<DraftAction> list, int index) = Locate(path);
            DraftAction copy = list[index].Clone();
            AssignNewIds(copy);
            list.Insert(index + 1, copy);
            created = Sibling(path, index + 1);
        });
        return created;
    }

    /// <summary>Swaps with the sibling above or below. Returns false at either end.</summary>
    public bool Move(string path, int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be -1 or 1.");
        }

        (List<DraftAction> list, int index) = Locate(path);
        int target = index + direction;
        if (target < 0 || target >= list.Count)
        {
            return false;
        }

        Commit(() =>
        {
            (List<DraftAction> live, int liveIndex) = Locate(path);
            (live[liveIndex], live[liveIndex + direction]) = (live[liveIndex + direction], live[liveIndex]);
        });
        return true;
    }

    public void SetField(string path, string field, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);
        DraftAction action = Locate(path).List[Locate(path).Index];
        if (!action.Fields.ContainsKey(field))
        {
            throw new ArgumentException("That field is not on this action.", nameof(field));
        }

        if (action.Fields[field] == value)
        {
            return;
        }

        Commit(() => Locate(path).List[Locate(path).Index].Fields[field] = value);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            throw new InvalidOperationException("Nothing to undo.");
        }

        _redo.Add(_draft.Clone());
        _draft = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Invalidate();
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            throw new InvalidOperationException("Nothing to redo.");
        }

        _undo.Add(_draft.Clone());
        _draft = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Invalidate();
    }

    /// <summary>Gives this macro a new id and a "Copy of" name, so saving does not replace the original.</summary>
    public void MakeCopy()
    {
        Commit(() =>
        {
            _draft.Id = "m" + Guid.NewGuid().ToString("N");
            _draft.Name = CopyName(_draft.Name);
        });
    }

    /// <summary>Writes the macro when it is valid. A problem leaves the folder untouched.</summary>
    public bool TrySave(string directory, out string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        path = "";
        if (Read().Document is not MacroDocument document)
        {
            return false;
        }

        path = MacroStore.Save(document, directory);
        _baseline = _draft.Clone();
        return true;
    }

    private void Commit(Action change)
    {
        Draft prior = _draft.Clone();
        change();
        if (Same(prior, _draft))
        {
            return;
        }

        _undo.Add(prior);
        if (_undo.Count > UndoLimit)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        Invalidate();
    }

    private bool IsRepeat(string path)
    {
        (List<DraftAction> list, int index) = Locate(path);
        return list[index].Type == "repeat.count";
    }

    private static string CopyName(string name)
    {
        string copy = "Copy of " + name.Trim();
        if (copy.Length > 120)
        {
            copy = copy[..120].TrimEnd();
        }

        return copy.Length == 0 ? "Copy" : copy;
    }

    private static DraftAction Stored(MacroAction action)
    {
        string typeId = StoredType(action);
        Dictionary<string, string> fields = Defaults(typeId);
        switch (action)
        {
            case MouseClickAction click:
                fields["button"] = ButtonName(click.Button);
                fields["coordinateMode"] = ModeName(click.Mode);
                fields["x"] = Number(click.X);
                fields["y"] = Number(click.Y);
                fields["clickCount"] = click.ClickCount.ToString(CultureInfo.InvariantCulture);
                break;
            case MouseMoveAction move:
                fields["coordinateMode"] = ModeName(move.Mode);
                fields["x"] = move.X.ToString(CultureInfo.InvariantCulture);
                fields["y"] = move.Y.ToString(CultureInfo.InvariantCulture);
                break;
            case MouseHoldAction hold:
                fields["button"] = ButtonName(hold.Button);
                fields["state"] = hold.State == MacroPressState.Up ? "up" : "down";
                fields["coordinateMode"] = hold.Mode is MacroCoordinateMode mode ? ModeName(mode) : "cursor";
                fields["x"] = Number(hold.X);
                fields["y"] = Number(hold.Y);
                break;
            case MouseWheelAction wheel:
                fields["notches"] = wheel.Notches.ToString(CultureInfo.InvariantCulture);
                break;
            case KeyboardKeyAction key:
                fields["key"] = key.Key;
                fields["state"] = key.State == MacroPressState.Up ? "up" : "down";
                break;
            case WaitAction wait:
                fields["durationMs"] = wait.DurationMs.ToString(CultureInfo.InvariantCulture);
                break;
            case RepeatCountAction repeat:
                fields["count"] = repeat.Count.ToString(CultureInfo.InvariantCulture);
                break;
            case CommentAction comment:
                fields["text"] = comment.Text;
                break;
        }

        if (fields.ContainsKey("comment"))
        {
            fields["comment"] = action.Comment ?? "";
        }

        var stored = new DraftAction
        {
            Id = action.Id,
            Type = typeId,
            Fields = fields,
        };
        if (action is RepeatCountAction loop)
        {
            foreach (MacroAction child in loop.Actions)
            {
                stored.Children.Add(Stored(child));
            }
        }

        return stored;
    }

    private static string StoredType(MacroAction action) => action switch
    {
        MouseClickAction => "mouse.click",
        MouseMoveAction => "mouse.move",
        MouseHoldAction => "mouse.hold",
        MouseWheelAction => "mouse.wheel",
        KeyboardKeyAction => "keyboard.key",
        WaitAction => "wait",
        RepeatCountAction => "repeat.count",
        CommentAction => "comment",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static string ButtonName(MacroMouseButton button) => button switch
    {
        MacroMouseButton.Right => "right",
        MacroMouseButton.Middle => "middle",
        _ => "left",
    };

    private static string ModeName(MacroCoordinateMode mode) => mode switch
    {
        MacroCoordinateMode.PrimaryDisplay => "primaryDisplay",
        MacroCoordinateMode.Relative => "relative",
        MacroCoordinateMode.Cursor => "cursor",
        _ => "virtualDesktop",
    };

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "0";

    private static int NextNumber(List<DraftAction> actions)
    {
        int next = 1;
        Walk(actions);
        return next;

        void Walk(List<DraftAction> list)
        {
            foreach (DraftAction action in list)
            {
                if (action.Id.StartsWith("step", StringComparison.Ordinal)
                    && int.TryParse(action.Id.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    && number >= next)
                {
                    next = number + 1;
                }

                Walk(action.Children);
            }
        }
    }

    private DraftAction CreateAction(string typeId)
    {
        return new DraftAction
        {
            Id = "step" + _draft.Next++.ToString(CultureInfo.InvariantCulture),
            Type = typeId,
            Fields = Defaults(typeId),
        };
    }

    private void AssignNewIds(DraftAction action)
    {
        action.Id = "step" + _draft.Next++.ToString(CultureInfo.InvariantCulture);
        foreach (DraftAction child in action.Children)
        {
            AssignNewIds(child);
        }
    }

    private (List<DraftAction> List, int Index) Locate(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("The action path is required.", nameof(path));
        }

        string[] parts = path.Split('/');
        List<DraftAction> list = _draft.Actions;
        for (int depth = 0; depth < parts.Length; depth++)
        {
            if (!int.TryParse(parts[depth], NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || index < 0
                || index >= list.Count)
            {
                throw new ArgumentException("That action is not in the list.", nameof(path));
            }

            if (depth == parts.Length - 1)
            {
                return (list, index);
            }

            if (list[index].Type != "repeat.count")
            {
                throw new ArgumentException("That action is not in the list.", nameof(path));
            }

            list = list[index].Children;
        }

        throw new ArgumentException("That action is not in the list.", nameof(path));
    }

    private MacroReadResult Read()
    {
        _read ??= MacroJson.Read(ToJson());
        return _read;
    }

    private void Invalidate() => _read = null;

    private string ToJson()
    {
        var document = new JsonObject
        {
            ["formatVersion"] = MacroFormat.CurrentVersion,
            ["id"] = _draft.Id,
            ["name"] = _draft.Name,
        };
        if (_draft.Description.Length > 0)
        {
            document["description"] = _draft.Description;
        }

        document["actions"] = WriteActions(_draft.Actions);
        return document.ToJsonString();
    }

    private static JsonArray WriteActions(List<DraftAction> actions)
    {
        var array = new JsonArray();
        foreach (DraftAction action in actions)
        {
            array.Add(WriteAction(action));
        }

        return array;
    }

    private static JsonObject WriteAction(DraftAction action)
    {
        var item = new JsonObject
        {
            ["id"] = action.Id,
            ["type"] = action.Type,
        };
        switch (action.Type)
        {
            case "mouse.click":
                Put(item, action, "button");
                Put(item, action, "coordinateMode");
                if (Value(action, "coordinateMode") != "cursor")
                {
                    PutPoint(item, action);
                }

                PutNumber(item, action, "clickCount");
                break;
            case "mouse.move":
                Put(item, action, "coordinateMode");
                PutNumber(item, action, "x");
                PutNumber(item, action, "y");
                break;
            case "mouse.hold":
                Put(item, action, "button");
                Put(item, action, "state");
                if (Value(action, "coordinateMode").Length > 0)
                {
                    Put(item, action, "coordinateMode");
                }

                if (Value(action, "coordinateMode") != "cursor")
                {
                    PutPoint(item, action);
                }

                break;
            case "mouse.wheel":
                PutNumber(item, action, "notches");
                break;
            case "keyboard.key":
                Put(item, action, "key");
                Put(item, action, "state");
                break;
            case "wait":
                PutNumber(item, action, "durationMs");
                break;
            case "repeat.count":
                PutNumber(item, action, "count");
                item["actions"] = WriteActions(action.Children);
                break;
            case "comment":
                Put(item, action, "text");
                break;
        }

        if (Value(action, "comment").Length > 0)
        {
            Put(item, action, "comment");
        }

        return item;
    }

    private static void PutPoint(JsonObject item, DraftAction action)
    {
        if (Value(action, "x").Length == 0 && Value(action, "y").Length == 0)
        {
            return;
        }

        PutNumber(item, action, "x");
        PutNumber(item, action, "y");
    }

    private static void Put(JsonObject item, DraftAction action, string field) => item[field] = Value(action, field);

    private static void PutNumber(JsonObject item, DraftAction action, string field)
    {
        string raw = Value(action, field);
        if (raw.Length == 0)
        {
            return;
        }

        item[field] = TryWhole(raw, out int number) ? number : raw;
    }

    private static bool TryWhole(string raw, out int number)
    {
        number = 0;
        if (raw.Length == 0)
        {
            return false;
        }

        int start = raw[0] == '-' ? 1 : 0;
        if (start == raw.Length)
        {
            return false;
        }

        for (int i = start; i < raw.Length; i++)
        {
            if (raw[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
    }

    private List<EditorStep> Flatten(List<DraftAction> actions, string prefix, int depth, IReadOnlyList<MacroIssue> issues)
    {
        var steps = new List<EditorStep>();
        for (int index = 0; index < actions.Count; index++)
        {
            DraftAction action = actions[index];
            string path = prefix.Length == 0 ? index.ToString(CultureInfo.InvariantCulture) : prefix + "/" + index.ToString(CultureInfo.InvariantCulture);
            var problems = new List<string>();
            foreach (MacroIssue issue in issues)
            {
                if (ActionPath(issue.Path) == path)
                {
                    problems.Add(issue.Message);
                }
            }

            steps.Add(new EditorStep(
                path,
                action.Id,
                action.Type,
                Title(action.Type),
                Category(action.Type),
                Line(action),
                depth,
                action.Type == "repeat.count",
                Fields(action),
                problems));
            if (action.Type == "repeat.count")
            {
                steps.AddRange(Flatten(action.Children, path, depth + 1, issues));
            }
        }

        return steps;
    }

    private static IReadOnlyList<EditorField> Fields(DraftAction action)
    {
        var fields = new List<EditorField>();
        foreach ((string name, string label, IReadOnlyList<EditorChoice> choices) in FieldPlan(action))
        {
            fields.Add(new EditorField(name, label, Value(action, name), Help(action.Type, name), choices));
        }

        return fields;
    }

    private static IEnumerable<(string Name, string Label, IReadOnlyList<EditorChoice> Choices)> FieldPlan(DraftAction action)
    {
        switch (action.Type)
        {
            case "mouse.click":
                yield return ("button", "Button", Buttons);
                yield return ("coordinateMode", "Where", Modes);
                if (Value(action, "coordinateMode") != "cursor")
                {
                    yield return ("x", "X", None);
                    yield return ("y", "Y", None);
                }

                yield return ("clickCount", "Clicks", Clicks);
                break;
            case "mouse.move":
                yield return ("coordinateMode", "Where", MoveModes);
                yield return ("x", "X", None);
                yield return ("y", "Y", None);
                break;
            case "mouse.hold":
                yield return ("button", "Button", Buttons);
                yield return ("state", "Down or up", PressStates);
                yield return ("coordinateMode", "Where", Modes);
                if (Value(action, "coordinateMode") != "cursor")
                {
                    yield return ("x", "X", None);
                    yield return ("y", "Y", None);
                }

                break;
            case "mouse.wheel":
                yield return ("notches", "Notches", None);
                break;
            case "keyboard.key":
                yield return ("key", "Key", Keys);
                yield return ("state", "Down or up", PressStates);
                break;
            case "wait":
                yield return ("durationMs", "Milliseconds", None);
                break;
            case "repeat.count":
                yield return ("count", "Times", None);
                break;
            case "comment":
                yield return ("text", "Text", None);
                yield break;
        }

        if (action.Fields.ContainsKey("comment"))
        {
            yield return ("comment", "Note", None);
        }
    }

    private static string Line(DraftAction action)
    {
        string button = Blank(Value(action, "button"));
        string mode = Value(action, "coordinateMode");
        string x = Value(action, "x");
        string y = Value(action, "y");
        return action.Type switch
        {
            "mouse.click" => ClickLine(button, mode, x, y, Value(action, "clickCount")),
            "mouse.move" => mode == "relative"
                ? $"Move the pointer by {Blank(x)}, {Blank(y)}."
                : $"Move the pointer to {Blank(x)}, {Blank(y)}.",
            "mouse.hold" => HoldLine(button, Value(action, "state"), mode, x, y),
            "mouse.wheel" => WheelLine(Value(action, "notches")),
            "keyboard.key" => Value(action, "state") == "up"
                ? $"Release {Blank(Value(action, "key"))}."
                : $"Press {Blank(Value(action, "key"))}.",
            "wait" => $"Wait {Blank(Value(action, "durationMs"))} ms.",
            "repeat.count" => $"Repeat {Blank(Value(action, "count"))} times.",
            "comment" => Value(action, "text").Length == 0 ? "Note." : "Note: " + Value(action, "text"),
            _ => action.Type,
        };
    }

    private static string ClickLine(string button, string mode, string x, string y, string count)
    {
        string times = count is "" or "1" ? "" : $", {count} times";
        return $"Click the {button} button {Where(mode, x, y)}{times}.";
    }

    private static string HoldLine(string button, string state, string mode, string x, string y)
    {
        string verb = state == "up" ? "Release" : "Hold";
        string tail = state == "up" ? "" : " down";
        string place = mode == "cursor" ? "" : " " + Where(mode, x, y);
        return $"{verb} the {button} button{tail}{place}.";
    }

    private static string WheelLine(string notches)
    {
        if (TryWhole(notches, out int count) && count < 0)
        {
            return $"Scroll {Math.Abs(count)} notches toward you.";
        }

        return $"Scroll {Blank(notches)} notches.";
    }

    private static string Where(string mode, string x, string y) => mode switch
    {
        "cursor" => "at the cursor",
        "relative" => $"by {Blank(x)}, {Blank(y)}",
        "primaryDisplay" => $"at {Blank(x)}, {Blank(y)} on the main screen",
        _ => $"at {Blank(x)}, {Blank(y)} on the desktop",
    };

    private static string Blank(string value) => value.Length == 0 ? "a missing value" : value;

    private static string Title(string typeId) =>
        MacroActionCatalog.TryGet(typeId, out MacroActionDescriptor descriptor) ? descriptor.DisplayName : typeId;

    private static string Category(string typeId) => typeId switch
    {
        "keyboard.key" => "Keyboard",
        "wait" => "Wait",
        "repeat.count" => "Flow",
        "comment" => "Note",
        _ => "Mouse",
    };

    private static string Help(string typeId, string field)
    {
        if (field == "comment")
        {
            return "A note for you. It is not run.";
        }

        if (!MacroActionCatalog.TryGet(typeId, out MacroActionDescriptor descriptor))
        {
            return "";
        }

        foreach (MacroParameterDescriptor parameter in descriptor.Parameters)
        {
            if (parameter.Name == field)
            {
                return parameter.Summary;
            }
        }

        return "";
    }

    private static string? ActionPath(string issuePath)
    {
        var parts = new List<string>();
        int index = 0;
        while (index < issuePath.Length)
        {
            int at = issuePath.IndexOf("actions[", index, StringComparison.Ordinal);
            if (at < 0 || (at != 0 && issuePath[at - 1] != '.'))
            {
                break;
            }

            int end = issuePath.IndexOf(']', at);
            if (end < 0)
            {
                break;
            }

            string number = issuePath[(at + "actions[".Length)..end];
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                break;
            }

            parts.Add(number);
            index = end + 1;
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    private static string Sibling(string path, int index)
    {
        int slash = path.LastIndexOf('/');
        string number = index.ToString(CultureInfo.InvariantCulture);
        return slash < 0 ? number : path[..slash] + "/" + number;
    }

    private static string Value(DraftAction action, string field) =>
        action.Fields.TryGetValue(field, out string? value) ? value : "";

    private static Dictionary<string, string> Defaults(string typeId) => typeId switch
    {
        "mouse.click" => Field("button", "left", "coordinateMode", "cursor", "x", "0", "y", "0", "clickCount", "1", "comment", ""),
        "mouse.move" => Field("coordinateMode", "virtualDesktop", "x", "0", "y", "0", "comment", ""),
        "mouse.hold" => Field("button", "left", "state", "down", "coordinateMode", "cursor", "x", "0", "y", "0", "comment", ""),
        "mouse.wheel" => Field("notches", "1", "comment", ""),
        "keyboard.key" => Field("key", "A", "state", "down", "comment", ""),
        "wait" => Field("durationMs", "200", "comment", ""),
        "repeat.count" => Field("count", "2", "comment", ""),
        "comment" => Field("text", "Note"),
        _ => throw new ArgumentOutOfRangeException(nameof(typeId)),
    };

    private static Dictionary<string, string> Field(params string[] pairs)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            fields[pairs[i]] = pairs[i + 1];
        }

        return fields;
    }

    private static bool Same(Draft left, Draft right)
    {
        if (left.Id != right.Id || left.Name != right.Name || left.Description != right.Description || left.Actions.Count != right.Actions.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Actions.Count; i++)
        {
            if (!Same(left.Actions[i], right.Actions[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Same(DraftAction left, DraftAction right)
    {
        if (left.Id != right.Id || left.Type != right.Type || left.Fields.Count != right.Fields.Count || left.Children.Count != right.Children.Count)
        {
            return false;
        }

        foreach ((string key, string value) in left.Fields)
        {
            if (!right.Fields.TryGetValue(key, out string? other) || other != value)
            {
                return false;
            }
        }

        for (int i = 0; i < left.Children.Count; i++)
        {
            if (!Same(left.Children[i], right.Children[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static readonly IReadOnlyList<EditorChoice> None = [];

    private static readonly IReadOnlyList<EditorChoice> Buttons =
    [
        new("left", "Left"),
        new("right", "Right"),
        new("middle", "Middle"),
    ];

    private static readonly IReadOnlyList<EditorChoice> Modes =
    [
        new("cursor", "At the cursor"),
        new("virtualDesktop", "On the desktop"),
        new("primaryDisplay", "On the main screen"),
        new("relative", "By a distance"),
    ];

    private static readonly IReadOnlyList<EditorChoice> MoveModes =
    [
        new("virtualDesktop", "On the desktop"),
        new("primaryDisplay", "On the main screen"),
        new("relative", "By a distance"),
    ];

    private static readonly IReadOnlyList<EditorChoice> PressStates =
    [
        new("down", "Down"),
        new("up", "Up"),
    ];

    private static readonly IReadOnlyList<EditorChoice> Clicks =
    [
        new("1", "1"),
        new("2", "2"),
        new("3", "3"),
    ];

    private static readonly IReadOnlyList<EditorChoice> Keys = BuildKeys();

    private static IReadOnlyList<EditorChoice> BuildKeys()
    {
        var keys = new EditorChoice[MacroKeys.All.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = new EditorChoice(MacroKeys.All[i], MacroKeys.All[i]);
        }

        return keys;
    }

    private sealed class Draft
    {
        public string Id = "";

        public string Name = "";

        public string Description = "";

        public int Next = 1;

        public List<DraftAction> Actions = [];

        public Draft Clone()
        {
            return new Draft
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Next = Next,
                Actions = Actions.ConvertAll(static action => action.Clone()),
            };
        }
    }

    private sealed class DraftAction
    {
        public string Id = "";

        public string Type = "";

        public Dictionary<string, string> Fields = new(StringComparer.Ordinal);

        public List<DraftAction> Children = [];

        public DraftAction Clone()
        {
            return new DraftAction
            {
                Id = Id,
                Type = Type,
                Fields = new Dictionary<string, string>(Fields, StringComparer.Ordinal),
                Children = Children.ConvertAll(static child => child.Clone()),
            };
        }
    }
}
