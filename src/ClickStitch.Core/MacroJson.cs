using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClickStitch.Core;

/// <summary>
/// Reads and writes a format-1 macro document. Reading validates the schema. Neither method runs actions.
/// </summary>
public static class MacroJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
        MaxDepth = 64,
    };

    public static MacroReadResult Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MacroFormat.MaxDocumentBytes)
        {
            return TooLarge();
        }

        return Read(Encoding.UTF8.GetBytes(json));
    }

    public static MacroReadResult Read(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MacroFormat.MaxDocumentBytes)
        {
            return TooLarge();
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(utf8, documentOptions: DocumentOptions);
        }
        catch (JsonException error)
        {
            return Fail("document", "The document is not valid JSON. " + error.Message);
        }

        if (root is not JsonObject document)
        {
            return Fail("document", "The document must be a JSON object.");
        }

        var reader = new MacroReader();
        MacroDocument? parsed = reader.ReadDocument(document);
        if (reader.Issues.Count > 0 || parsed is null)
        {
            return new MacroReadResult(null, reader.Issues);
        }

        return new MacroReadResult(parsed, []);
    }

    public static string Write(MacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteDocument(writer, document);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static MacroReadResult TooLarge() =>
        Fail("document", "The document is larger than 2 MiB.");

    private static MacroReadResult Fail(string path, string message) =>
        new(null, [new MacroIssue(path, message)]);

    private static void WriteDocument(Utf8JsonWriter writer, MacroDocument document)
    {
        writer.WriteStartObject();
        writer.WriteNumber("formatVersion", MacroFormat.CurrentVersion);
        writer.WriteString("id", document.Id);
        writer.WriteString("name", document.Name);
        if (document.DescriptionSpecified)
        {
            writer.WriteString("description", document.Description);
        }

        if (document.Metadata is not null)
        {
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            if (document.Metadata.CreatedUtc is not null)
            {
                writer.WriteString("createdUtc", document.Metadata.CreatedUtc);
            }

            if (document.Metadata.ModifiedUtc is not null)
            {
                writer.WriteString("modifiedUtc", document.Metadata.ModifiedUtc);
            }

            WriteExtras(writer, document.Metadata.Extras);
            writer.WriteEndObject();
        }

        if (document.CoordinateSpace is not null)
        {
            writer.WritePropertyName("coordinateSpace");
            writer.WriteStartObject();
            writer.WriteString("system", document.CoordinateSpace.System);
            WriteExtras(writer, document.CoordinateSpace.Extras);
            writer.WriteEndObject();
        }

        if (document.Settings is not null)
        {
            writer.WritePropertyName("settings");
            writer.WriteStartObject();
            if (document.Settings.StopOnErrorSpecified)
            {
                writer.WriteBoolean("stopOnError", document.Settings.StopOnError);
            }

            WriteExtras(writer, document.Settings.Extras);
            writer.WriteEndObject();
        }

        if (document.VariablesSpecified)
        {
            writer.WritePropertyName("variables");
            writer.WriteStartArray();
            foreach (MacroVariable variable in document.Variables)
            {
                writer.WriteStartObject();
                writer.WriteString("name", variable.Name);
                writer.WriteString("type", VariableType(variable.Type));
                writer.WritePropertyName("default");
                variable.Default.WriteTo(writer);
                WriteExtras(writer, variable.Extras);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (document.PluginDependenciesSpecified)
        {
            writer.WritePropertyName("pluginDependencies");
            writer.WriteStartArray();
            writer.WriteEndArray();
        }

        writer.WritePropertyName("actions");
        WriteActions(writer, document.Actions);
        WriteExtras(writer, document.Extras);
        writer.WriteEndObject();
    }

    private static void WriteActions(Utf8JsonWriter writer, IReadOnlyList<MacroAction> actions)
    {
        writer.WriteStartArray();
        foreach (MacroAction action in actions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", action.Id);
            writer.WriteString("type", ActionType(action));
            switch (action)
            {
                case MouseClickAction click:
                    writer.WriteString("button", Button(click.Button));
                    if (click.ModeSpecified)
                    {
                        writer.WriteString("coordinateMode", Mode(click.Mode));
                    }

                    if (click.X is int x && click.Y is int y)
                    {
                        writer.WriteNumber("x", x);
                        writer.WriteNumber("y", y);
                    }

                    if (click.ClickCountSpecified)
                    {
                        writer.WriteNumber("clickCount", click.ClickCount);
                    }

                    break;
                case MouseMoveAction move:
                    if (move.ModeSpecified)
                    {
                        writer.WriteString("coordinateMode", Mode(move.Mode));
                    }

                    writer.WriteNumber("x", move.X);
                    writer.WriteNumber("y", move.Y);
                    break;
                case MouseHoldAction hold:
                    writer.WriteString("button", Button(hold.Button));
                    writer.WriteString("state", Press(hold.State));
                    if (hold.ModeSpecified && hold.Mode is MacroCoordinateMode mode)
                    {
                        writer.WriteString("coordinateMode", Mode(mode));
                    }

                    if (hold.X is int holdX && hold.Y is int holdY)
                    {
                        writer.WriteNumber("x", holdX);
                        writer.WriteNumber("y", holdY);
                    }

                    break;
                case MouseWheelAction wheel:
                    writer.WriteNumber("notches", wheel.Notches);
                    break;
                case KeyboardKeyAction key:
                    writer.WriteString("key", key.Key);
                    writer.WriteString("state", Press(key.State));
                    break;
                case WaitAction wait:
                    writer.WriteNumber("durationMs", wait.DurationMs);
                    break;
                case RepeatCountAction repeat:
                    writer.WriteNumber("count", repeat.Count);
                    writer.WritePropertyName("actions");
                    WriteActions(writer, repeat.Actions);
                    break;
                case CommentAction comment:
                    writer.WriteString("text", comment.Text);
                    break;
            }

            if (action.Comment is not null)
            {
                writer.WriteString("comment", action.Comment);
            }

            WriteExtras(writer, action.Extras);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteExtras(Utf8JsonWriter writer, IReadOnlyList<MacroExtra> extras)
    {
        foreach (MacroExtra extra in extras)
        {
            writer.WritePropertyName(extra.Name);
            extra.Value.WriteTo(writer);
        }
    }

    private static string ActionType(MacroAction action) => action switch
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

    private static string Button(MacroMouseButton button) => button switch
    {
        MacroMouseButton.Left => "left",
        MacroMouseButton.Right => "right",
        MacroMouseButton.Middle => "middle",
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private static string Mode(MacroCoordinateMode mode) => mode switch
    {
        MacroCoordinateMode.VirtualDesktop => "virtualDesktop",
        MacroCoordinateMode.PrimaryDisplay => "primaryDisplay",
        MacroCoordinateMode.Relative => "relative",
        MacroCoordinateMode.Cursor => "cursor",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static string Press(MacroPressState state) => state switch
    {
        MacroPressState.Down => "down",
        MacroPressState.Up => "up",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string VariableType(MacroVariableType type) => type switch
    {
        MacroVariableType.Number => "number",
        MacroVariableType.Text => "text",
        MacroVariableType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private sealed class MacroReader
    {
        private readonly HashSet<string> _actionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _variableNames = new(StringComparer.Ordinal);

        internal List<MacroIssue> Issues { get; } = [];

        internal MacroDocument? ReadDocument(JsonObject document)
        {
            if (!TryFormatVersion(document))
            {
                return null;
            }

            string? id = ReadId(document, "id");
            string? name = ReadName(document);
            (string description, bool descriptionSpecified) = ReadDescription(document);
            MacroMetadata? metadata = ReadMetadata(document);
            MacroCoordinateSpace? coordinateSpace = ReadCoordinateSpace(document);
            MacroSettings? settings = ReadSettings(document);
            (IReadOnlyList<MacroVariable> variables, bool variablesSpecified) = ReadVariables(document);
            bool pluginsSpecified = ReadPluginDependencies(document);
            IReadOnlyList<MacroAction> actions = ReadActionArray(document, "actions", required: true, repeatDepth: 0);
            List<MacroExtra> extras = ReadExtras(document, KnownRoot);

            if (Issues.Count > 0 || id is null || name is null)
            {
                return null;
            }

            return new MacroDocument(
                id,
                name,
                description,
                descriptionSpecified,
                metadata,
                coordinateSpace,
                settings,
                variables,
                variablesSpecified,
                pluginsSpecified,
                actions,
                extras);
        }

        private bool TryFormatVersion(JsonObject document)
        {
            if (!document.ContainsKey("formatVersion"))
            {
                Issue("formatVersion", "Format version is required.");
                return false;
            }

            if (!TryInteger(document["formatVersion"], out int version))
            {
                Issue("formatVersion", "Format version must be the integer 1.");
                return false;
            }

            if (!MacroFormat.IsSupported(version))
            {
                Issue("formatVersion", "Format version must be 1. This reader does not guess at other versions.");
                return false;
            }

            return true;
        }

        private string? ReadId(JsonObject document, string path)
        {
            if (!document.ContainsKey("id"))
            {
                Issue(path, "Id is required.");
                return null;
            }

            if (!TryString(document["id"], out string? id))
            {
                Issue(path, "Id must be text.");
                return null;
            }

            if (!IsMacroId(id))
            {
                Issue(path, "Id must be 1–64 characters and use only letters, digits, '.', '_', and '-'.");
                return null;
            }

            return id;
        }

        private string? ReadName(JsonObject document)
        {
            if (!document.ContainsKey("name"))
            {
                Issue("name", "Name is required.");
                return null;
            }

            if (!TryString(document["name"], out string? name))
            {
                Issue("name", "Name must be text.");
                return null;
            }

            if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                Issue("name", "Name must not start or end with whitespace.");
                return null;
            }

            if (name.Length is < 1 or > 120)
            {
                Issue("name", "Name must be 1–120 characters.");
                return null;
            }

            return name;
        }

        private (string Description, bool Specified) ReadDescription(JsonObject document)
        {
            if (!document.ContainsKey("description"))
            {
                return ("", false);
            }

            if (!TryString(document["description"], out string? description))
            {
                Issue("description", "Description must be text.");
                return ("", true);
            }

            if (description.Length > 2000)
            {
                Issue("description", "Description must be at most 2000 characters.");
            }

            return (description, true);
        }

        private MacroMetadata? ReadMetadata(JsonObject document)
        {
            if (!document.ContainsKey("metadata"))
            {
                return null;
            }

            if (document["metadata"] is not JsonObject metadata)
            {
                Issue("metadata", "Metadata must be an object.");
                return null;
            }

            string? created = ReadTimestamp(metadata, "metadata.createdUtc", "createdUtc");
            string? modified = ReadTimestamp(metadata, "metadata.modifiedUtc", "modifiedUtc");
            return new MacroMetadata(created, modified, ReadExtras(metadata, KnownMetadata));
        }

        private string? ReadTimestamp(JsonObject metadata, string path, string name)
        {
            if (!metadata.ContainsKey(name))
            {
                return null;
            }

            if (!TryString(metadata[name], out string? text))
            {
                Issue(path, "Must be an ISO-8601 timestamp in UTC, such as 2026-11-02T20:00:00Z.");
                return null;
            }

            bool utc = text.Contains('T', StringComparison.Ordinal)
                && (text.EndsWith("Z", StringComparison.Ordinal)
                    || text.EndsWith("+00:00", StringComparison.Ordinal)
                    || text.EndsWith("-00:00", StringComparison.Ordinal));
            if (!utc || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed)
                || parsed.Offset != TimeSpan.Zero)
            {
                Issue(path, "Must be an ISO-8601 timestamp in UTC, such as 2026-11-02T20:00:00Z.");
                return null;
            }

            return text;
        }

        private MacroCoordinateSpace? ReadCoordinateSpace(JsonObject document)
        {
            if (!document.ContainsKey("coordinateSpace"))
            {
                return null;
            }

            if (document["coordinateSpace"] is not JsonObject space)
            {
                Issue("coordinateSpace", "Coordinate space must be an object.");
                return null;
            }

            if (!space.ContainsKey("system"))
            {
                Issue("coordinateSpace.system", "Coordinate system is required when coordinateSpace is present.");
            }
            else if (!TryString(space["system"], out string? system) || system != "virtualDesktopPixels")
            {
                Issue("coordinateSpace.system", "Format 1 coordinate system must be virtualDesktopPixels.");
            }

            return new MacroCoordinateSpace(ReadExtras(space, KnownCoordinateSpace));
        }

        private MacroSettings? ReadSettings(JsonObject document)
        {
            if (!document.ContainsKey("settings"))
            {
                return null;
            }

            if (document["settings"] is not JsonObject settings)
            {
                Issue("settings", "Settings must be an object.");
                return null;
            }

            bool specified = settings.ContainsKey("stopOnError");
            bool stopOnError = true;
            if (specified)
            {
                if (settings["stopOnError"] is JsonValue value && value.TryGetValue<bool>(out bool parsed))
                {
                    stopOnError = parsed;
                }
                else
                {
                    Issue("settings.stopOnError", "stopOnError must be true or false.");
                }
            }

            return new MacroSettings(stopOnError, specified, ReadExtras(settings, KnownSettings));
        }

        private (IReadOnlyList<MacroVariable> Variables, bool Specified) ReadVariables(JsonObject document)
        {
            if (!document.ContainsKey("variables"))
            {
                return ([], false);
            }

            if (document["variables"] is not JsonArray array)
            {
                Issue("variables", "Variables must be an array.");
                return ([], true);
            }

            var variables = new List<MacroVariable>();
            for (int index = 0; index < array.Count; index++)
            {
                string path = $"variables[{index}]";
                if (array[index] is not JsonObject item)
                {
                    Issue(path, "A variable must be an object.");
                    continue;
                }

                string? name = RequiredString(item, path + ".name", "name", "Variable name is required.", "Variable name must be text.");
                if (name is not null && !IsVariableName(name))
                {
                    Issue(path + ".name", "Variable name must start with a letter and then use up to 31 letters, digits, or underscores.");
                    name = null;
                }

                if (name is not null && !_variableNames.Add(name))
                {
                    Issue(path + ".name", "This variable name is already used.");
                }

                MacroVariableType? type = ReadVariableType(item, path);
                JsonNode? defaultValue = ReadVariableDefault(item, path, type);
                if (name is null || type is null || defaultValue is null)
                {
                    continue;
                }

                variables.Add(new MacroVariable(name, type.Value, Clone(defaultValue), ReadExtras(item, KnownVariable)));
            }

            return (variables, true);
        }

        private MacroVariableType? ReadVariableType(JsonObject item, string path)
        {
            if (!item.ContainsKey("type"))
            {
                Issue(path + ".type", "Variable type is required.");
                return null;
            }

            if (!TryString(item["type"], out string? type))
            {
                Issue(path + ".type", "Variable type must be number, text, or boolean.");
                return null;
            }

            return type switch
            {
                "number" => MacroVariableType.Number,
                "text" => MacroVariableType.Text,
                "boolean" => MacroVariableType.Boolean,
                _ => IssueType(path),
            };
        }

        private MacroVariableType? IssueType(string path)
        {
            Issue(path + ".type", "Variable type must be number, text, or boolean.");
            return null;
        }

        private JsonNode? ReadVariableDefault(JsonObject item, string path, MacroVariableType? type)
        {
            if (!item.ContainsKey("default"))
            {
                Issue(path + ".default", "Variable default is required.");
                return null;
            }

            JsonNode? node = item["default"];
            if (type is null)
            {
                return null;
            }

            switch (type.Value)
            {
                case MacroVariableType.Number:
                    if (node is not JsonValue number || number.GetValueKind() != JsonValueKind.Number
                        || !number.TryGetValue<double>(out double parsed) || !double.IsFinite(parsed))
                    {
                        Issue(path + ".default", "Default must be a finite number.");
                        return null;
                    }

                    return node;
                case MacroVariableType.Text:
                    if (!TryString(node, out string? text))
                    {
                        Issue(path + ".default", "Default must be text.");
                        return null;
                    }

                    if (text.Length > 2000)
                    {
                        Issue(path + ".default", "Default text must be at most 2000 characters.");
                        return null;
                    }

                    return node;
                case MacroVariableType.Boolean:
                    if (node is not JsonValue flag || !flag.TryGetValue<bool>(out _))
                    {
                        Issue(path + ".default", "Default must be true or false.");
                        return null;
                    }

                    return node;
                default:
                    return null;
            }
        }

        private bool ReadPluginDependencies(JsonObject document)
        {
            if (!document.ContainsKey("pluginDependencies"))
            {
                return false;
            }

            if (document["pluginDependencies"] is not JsonArray array)
            {
                Issue("pluginDependencies", "Plugin dependencies must be an array.");
                return true;
            }

            if (array.Count > 0)
            {
                Issue("pluginDependencies", "Format 1 does not allow plugin dependencies.");
            }

            return true;
        }

        private IReadOnlyList<MacroAction> ReadActionArray(JsonObject owner, string propertyName, bool required, int repeatDepth)
        {
            string path = propertyName;
            if (!owner.ContainsKey("actions"))
            {
                if (required)
                {
                    Issue(path, "Actions are required.");
                }

                return [];
            }

            if (owner["actions"] is not JsonArray array)
            {
                Issue(path, "Actions must be an array.");
                return [];
            }

            var actions = new List<MacroAction>();
            for (int index = 0; index < array.Count; index++)
            {
                string actionPath = $"{path}[{index}]";
                if (array[index] is not JsonObject item)
                {
                    Issue(actionPath, "An action must be an object.");
                    continue;
                }

                MacroAction? action = ReadAction(item, actionPath, repeatDepth);
                if (action is not null)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        private MacroAction? ReadAction(JsonObject item, string path, int repeatDepth)
        {
            string? id = ReadActionId(item, path);
            string? type = ReadActionType(item, path);
            string? comment = ReadActionComment(item, path);
            if (id is null || type is null)
            {
                return null;
            }

            MacroAction? action = type switch
            {
                "mouse.click" => ReadClick(item, path, id, comment),
                "mouse.move" => ReadMove(item, path, id, comment),
                "mouse.hold" => ReadHold(item, path, id, comment),
                "mouse.wheel" => ReadWheel(item, path, id, comment),
                "keyboard.key" => ReadKey(item, path, id, comment),
                "wait" => ReadWait(item, path, id, comment),
                "repeat.count" => ReadRepeat(item, path, id, comment, repeatDepth),
                "comment" => ReadComment(item, path, id, comment),
                "repeat.until" or "branch.if" => Reserved(path, type),
                _ => Unknown(path, type),
            };
            return action;
        }

        private string? ReadActionId(JsonObject item, string path)
        {
            if (!item.ContainsKey("id"))
            {
                Issue(path + ".id", "Action id is required.");
                return null;
            }

            if (!TryString(item["id"], out string? id))
            {
                Issue(path + ".id", "Action id must be text.");
                return null;
            }

            if (!IsMacroId(id))
            {
                Issue(path + ".id", "Id must be 1–64 characters and use only letters, digits, '.', '_', and '-'.");
                return null;
            }

            if (!_actionIds.Add(id))
            {
                Issue(path + ".id", "This id is already used.");
                return null;
            }

            return id;
        }

        private string? ReadActionType(JsonObject item, string path)
        {
            if (!item.ContainsKey("type"))
            {
                Issue(path + ".type", "Action type is required.");
                return null;
            }

            if (!TryString(item["type"], out string? type))
            {
                Issue(path + ".type", "Action type must be text.");
                return null;
            }

            return type;
        }

        private string? ReadActionComment(JsonObject item, string path)
        {
            if (!item.ContainsKey("comment"))
            {
                return null;
            }

            if (!TryString(item["comment"], out string? comment))
            {
                Issue(path + ".comment", "Comment must be text.");
                return null;
            }

            if (comment.Length > 500)
            {
                Issue(path + ".comment", "Comment must be at most 500 characters.");
                return null;
            }

            return comment;
        }

        private MacroAction? ReadClick(JsonObject item, string path, string id, string? comment)
        {
            if (!TryButton(item, path, out MacroMouseButton button))
            {
                return null;
            }

            if (!TryPoint(item, path, PointRule.RequiredUnlessCursor, out MacroCoordinateMode? mode, out bool modeSpecified, out int? x, out int? y))
            {
                return null;
            }

            bool countSpecified = item.ContainsKey("clickCount");
            int clickCount = 1;
            if (countSpecified)
            {
                if (!TryInteger(item["clickCount"], out clickCount) || clickCount is < 1 or > 3)
                {
                    Issue(path + ".clickCount", "Click count must be an integer from 1 to 3.");
                    return null;
                }
            }

            return new MouseClickAction(id, comment, ReadExtras(item, KnownClick), button, mode!.Value, modeSpecified, x, y, clickCount, countSpecified);
        }

        private MacroAction? ReadMove(JsonObject item, string path, string id, string? comment)
        {
            if (!TryPoint(item, path, PointRule.AlwaysRequired, out MacroCoordinateMode? mode, out bool modeSpecified, out int? x, out int? y))
            {
                return null;
            }

            return new MouseMoveAction(id, comment, ReadExtras(item, KnownMove), mode!.Value, modeSpecified, x!.Value, y!.Value);
        }

        private MacroAction? ReadHold(JsonObject item, string path, string id, string? comment)
        {
            if (!TryButton(item, path, out MacroMouseButton button) || !TryPress(item, path, out MacroPressState state))
            {
                return null;
            }

            if (!TryPoint(item, path, PointRule.Optional, out MacroCoordinateMode? mode, out bool modeSpecified, out int? x, out int? y))
            {
                return null;
            }

            return new MouseHoldAction(id, comment, ReadExtras(item, KnownHold), button, state, mode, modeSpecified, x, y);
        }

        private MacroAction? ReadWheel(JsonObject item, string path, string id, string? comment)
        {
            if (!item.ContainsKey("notches") || !TryInteger(item["notches"], out int notches) || notches is 0 or < -100 or > 100)
            {
                Issue(path + ".notches", "Notches must be an integer from -100 to 100 and cannot be 0.");
                return null;
            }

            return new MouseWheelAction(id, comment, ReadExtras(item, KnownWheel), notches);
        }

        private MacroAction? ReadKey(JsonObject item, string path, string id, string? comment)
        {
            if (!item.ContainsKey("key") || !TryString(item["key"], out string? key) || !MacroKeys.IsKnown(key))
            {
                Issue(path + ".key", "Key must be one of the format 1 key names.");
                return null;
            }

            if (!TryPress(item, path, out MacroPressState state))
            {
                return null;
            }

            return new KeyboardKeyAction(id, comment, ReadExtras(item, KnownKey), key, state);
        }

        private MacroAction? ReadWait(JsonObject item, string path, string id, string? comment)
        {
            if (!item.ContainsKey("durationMs") || !TryInteger(item["durationMs"], out int duration) || duration is < 0 or > 3_600_000)
            {
                Issue(path + ".durationMs", "Wait must be an integer from 0 to 3600000 milliseconds.");
                return null;
            }

            return new WaitAction(id, comment, ReadExtras(item, KnownWait), duration);
        }

        private MacroAction? ReadRepeat(JsonObject item, string path, string id, string? comment, int repeatDepth)
        {
            if (!item.ContainsKey("count") || !TryInteger(item["count"], out int count) || count is < 1 or > 1_000_000)
            {
                Issue(path + ".count", "Repeat count must be an integer from 1 to 1000000.");
                return null;
            }

            if (repeatDepth + 1 > 8)
            {
                Issue(path, "Repeat cannot be nested more than 8 levels.");
                return null;
            }

            if (!item.ContainsKey("actions"))
            {
                Issue(path + ".actions", "Repeat actions are required.");
                return null;
            }

            if (item["actions"] is not JsonArray)
            {
                Issue(path + ".actions", "Actions must be an array.");
                return null;
            }

            IReadOnlyList<MacroAction> actions = ReadNestedActions(item, path, repeatDepth + 1);
            return new RepeatCountAction(id, comment, ReadExtras(item, KnownRepeat), count, actions);
        }

        private IReadOnlyList<MacroAction> ReadNestedActions(JsonObject owner, string path, int repeatDepth)
        {
            var actions = new List<MacroAction>();
            var array = (JsonArray)owner["actions"]!;
            for (int index = 0; index < array.Count; index++)
            {
                string actionPath = $"{path}.actions[{index}]";
                if (array[index] is not JsonObject item)
                {
                    Issue(actionPath, "An action must be an object.");
                    continue;
                }

                MacroAction? action = ReadAction(item, actionPath, repeatDepth);
                if (action is not null)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        private MacroAction? ReadComment(JsonObject item, string path, string id, string? comment)
        {
            if (!item.ContainsKey("text") || !TryString(item["text"], out string? text))
            {
                Issue(path + ".text", "Comment text is required.");
                return null;
            }

            if (text.Length > 2000)
            {
                Issue(path + ".text", "Comment text must be at most 2000 characters.");
                return null;
            }

            return new CommentAction(id, comment, ReadExtras(item, KnownComment), text);
        }

        private MacroAction? Reserved(string path, string type)
        {
            Issue(path + ".type", $"{type} is not valid in format 1.");
            return null;
        }

        private MacroAction? Unknown(string path, string type)
        {
            Issue(path + ".type", $"{type} is not a format 1 action.");
            return null;
        }

        private bool TryButton(JsonObject item, string path, out MacroMouseButton button)
        {
            button = default;
            if (!item.ContainsKey("button") || !TryString(item["button"], out string? text))
            {
                Issue(path + ".button", "Button must be left, right, or middle.");
                return false;
            }

            switch (text)
            {
                case "left":
                    button = MacroMouseButton.Left;
                    return true;
                case "right":
                    button = MacroMouseButton.Right;
                    return true;
                case "middle":
                    button = MacroMouseButton.Middle;
                    return true;
                default:
                    Issue(path + ".button", "Button must be left, right, or middle.");
                    return false;
            }
        }

        private bool TryPress(JsonObject item, string path, out MacroPressState state)
        {
            state = default;
            if (!item.ContainsKey("state") || !TryString(item["state"], out string? text))
            {
                Issue(path + ".state", "State must be down or up.");
                return false;
            }

            switch (text)
            {
                case "down":
                    state = MacroPressState.Down;
                    return true;
                case "up":
                    state = MacroPressState.Up;
                    return true;
                default:
                    Issue(path + ".state", "State must be down or up.");
                    return false;
            }
        }

        private bool TryPoint(
            JsonObject item,
            string path,
            PointRule rule,
            out MacroCoordinateMode? mode,
            out bool modeSpecified,
            out int? x,
            out int? y)
        {
            mode = null;
            modeSpecified = item.ContainsKey("coordinateMode");
            x = null;
            y = null;
            bool sawX = item.ContainsKey("x");
            bool sawY = item.ContainsKey("y");
            if (modeSpecified)
            {
                if (!TryString(item["coordinateMode"], out string? text) || !TryMode(text, out MacroCoordinateMode parsed))
                {
                    Issue(path + ".coordinateMode", "Coordinate mode must be virtualDesktop, primaryDisplay, relative, or cursor.");
                    return false;
                }

                mode = parsed;
            }

            if (sawX != sawY)
            {
                Issue(path + ".x", "x and y must both be present.");
                return false;
            }

            if (sawX)
            {
                if (!TryInteger(item["x"], out int parsedX) || parsedX is < -32768 or > 32767)
                {
                    Issue(path + ".x", "x must be an integer from -32768 to 32767.");
                    return false;
                }

                if (!TryInteger(item["y"], out int parsedY) || parsedY is < -32768 or > 32767)
                {
                    Issue(path + ".y", "y must be an integer from -32768 to 32767.");
                    return false;
                }

                x = parsedX;
                y = parsedY;
            }

            if (rule == PointRule.AlwaysRequired && mode == MacroCoordinateMode.Cursor)
            {
                Issue(path + ".coordinateMode", "mouse.move cannot use coordinateMode cursor.");
                return false;
            }

            if (mode == MacroCoordinateMode.Cursor && sawX)
            {
                Issue(path + ".x", "cursor cannot include x or y.");
                return false;
            }

            if (rule == PointRule.AlwaysRequired && !sawX)
            {
                Issue(path + ".x", "x and y are required.");
                return false;
            }

            if (rule == PointRule.RequiredUnlessCursor)
            {
                MacroCoordinateMode effective = mode ?? MacroCoordinateMode.VirtualDesktop;
                if (effective == MacroCoordinateMode.Cursor && sawX)
                {
                    return false;
                }

                if (effective != MacroCoordinateMode.Cursor && !sawX)
                {
                    Issue(path + ".x", "A point is required unless coordinateMode is cursor.");
                    return false;
                }

                mode = effective;
            }

            if (rule == PointRule.Optional && !sawX && mode is not null and not MacroCoordinateMode.Cursor)
            {
                Issue(path + ".x", "A point is required for this coordinate mode.");
                return false;
            }

            if (rule != PointRule.Optional && mode is null)
            {
                mode = MacroCoordinateMode.VirtualDesktop;
            }

            if (rule == PointRule.Optional && sawX && mode is null)
            {
                mode = MacroCoordinateMode.VirtualDesktop;
            }

            return true;
        }

        private static bool TryMode(string text, out MacroCoordinateMode mode)
        {
            switch (text)
            {
                case "virtualDesktop":
                    mode = MacroCoordinateMode.VirtualDesktop;
                    return true;
                case "primaryDisplay":
                    mode = MacroCoordinateMode.PrimaryDisplay;
                    return true;
                case "relative":
                    mode = MacroCoordinateMode.Relative;
                    return true;
                case "cursor":
                    mode = MacroCoordinateMode.Cursor;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        private List<MacroExtra> ReadExtras(JsonObject item, IReadOnlySet<string> known)
        {
            var extras = new List<MacroExtra>();
            foreach (KeyValuePair<string, JsonNode?> pair in item)
            {
                if (known.Contains(pair.Key))
                {
                    continue;
                }

                extras.Add(new MacroExtra(pair.Key, Clone(pair.Value)));
            }

            return extras;
        }

        private string? RequiredString(JsonObject item, string path, string name, string missing, string wrongType)
        {
            if (!item.ContainsKey(name))
            {
                Issue(path, missing);
                return null;
            }

            if (!TryString(item[name], out string? text))
            {
                Issue(path, wrongType);
                return null;
            }

            return text;
        }

        private void Issue(string path, string message) => Issues.Add(new MacroIssue(path, message));
    }

    private enum PointRule
    {
        RequiredUnlessCursor,
        AlwaysRequired,
        Optional,
    }

    private static readonly HashSet<string> KnownRoot = new(StringComparer.Ordinal)
    {
        "formatVersion", "id", "name", "description", "metadata", "coordinateSpace", "settings", "variables", "pluginDependencies", "actions",
    };

    private static readonly HashSet<string> KnownMetadata = new(StringComparer.Ordinal)
    {
        "createdUtc", "modifiedUtc",
    };

    private static readonly HashSet<string> KnownCoordinateSpace = new(StringComparer.Ordinal)
    {
        "system",
    };

    private static readonly HashSet<string> KnownSettings = new(StringComparer.Ordinal)
    {
        "stopOnError",
    };

    private static readonly HashSet<string> KnownVariable = new(StringComparer.Ordinal)
    {
        "name", "type", "default",
    };

    private static readonly HashSet<string> KnownClick = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "button", "coordinateMode", "x", "y", "clickCount",
    };

    private static readonly HashSet<string> KnownMove = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "coordinateMode", "x", "y",
    };

    private static readonly HashSet<string> KnownHold = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "button", "state", "coordinateMode", "x", "y",
    };

    private static readonly HashSet<string> KnownWheel = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "notches",
    };

    private static readonly HashSet<string> KnownKey = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "key", "state",
    };

    private static readonly HashSet<string> KnownWait = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "durationMs",
    };

    private static readonly HashSet<string> KnownRepeat = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "count", "actions",
    };

    private static readonly HashSet<string> KnownComment = new(StringComparer.Ordinal)
    {
        "id", "type", "comment", "text",
    };

    private static bool TryString(JsonNode? node, out string value)
    {
        if (node is JsonValue json && json.TryGetValue<string>(out string? text) && text is not null)
        {
            value = text;
            return true;
        }

        value = "";
        return false;
    }

    private static bool TryInteger(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue json || json.GetValueKind() != JsonValueKind.Number)
        {
            return false;
        }

        string raw = json.ToJsonString();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
        {
            return false;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsMacroId(string value)
    {
        if (value.Length is < 1 or > 64)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVariableName(string value)
    {
        if (value.Length is < 1 or > 32)
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsAsciiLetter(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static JsonNode Clone(JsonNode? node) => node?.DeepClone() ?? JsonNode.Parse("null")!;
}

/// <summary>
/// The outcome of reading a macro document. A valid result has a document and no issues.
/// </summary>
public sealed class MacroReadResult
{
    internal MacroReadResult(MacroDocument? document, IReadOnlyList<MacroIssue> issues)
    {
        Document = document;
        Issues = issues;
    }

    public MacroDocument? Document { get; }

    public IReadOnlyList<MacroIssue> Issues { get; }

    public bool IsValid => Document is not null;
}
