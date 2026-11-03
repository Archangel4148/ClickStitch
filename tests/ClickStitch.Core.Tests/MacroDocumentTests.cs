namespace ClickStitch.Core.Tests;

public sealed class MacroDocumentTests
{
    private const string Example = """
        {
          "formatVersion": 1,
          "id": "sample-click-sequence",
          "name": "Sample Click Sequence",
          "description": "Clicks two points, with a short wait between them.",
          "coordinateSpace": { "system": "virtualDesktopPixels" },
          "settings": { "stopOnError": true },
          "variables": [],
          "pluginDependencies": [],
          "actions": [
            {
              "id": "click-start",
              "type": "mouse.click",
              "button": "left",
              "coordinateMode": "virtualDesktop",
              "x": 500,
              "y": 300
            },
            {
              "id": "pause-briefly",
              "type": "wait",
              "durationMs": 100
            },
            {
              "id": "click-next",
              "type": "mouse.click",
              "button": "right",
              "coordinateMode": "cursor"
            }
          ]
        }
        """;

    [Fact]
    public void Format_1_has_ninety_key_names()
    {
        Assert.Equal(90, MacroKeys.All.Count);
        Assert.True(MacroKeys.IsKnown("F13"));
        Assert.False(MacroKeys.IsKnown("a"));
        Assert.False(MacroKeys.IsKnown("Numpad0"));
    }

    [Fact]
    public void The_schema_example_reads_and_round_trips()
    {
        MacroReadResult result = MacroJson.Read(Example);

        Assert.True(result.IsValid);
        MacroDocument document = result.Document!;
        Assert.Equal(1, document.FormatVersion);
        Assert.Equal("sample-click-sequence", document.Id);
        Assert.Equal("Sample Click Sequence", document.Name);
        Assert.Equal("Clicks two points, with a short wait between them.", document.Description);
        Assert.True(document.StopOnError);
        Assert.Equal("virtualDesktopPixels", document.CoordinateSpace!.System);
        Assert.Empty(document.Variables);

        MouseClickAction first = Assert.IsType<MouseClickAction>(document.Actions[0]);
        Assert.Equal(MacroMouseButton.Left, first.Button);
        Assert.Equal(MacroCoordinateMode.VirtualDesktop, first.Mode);
        Assert.Equal(500, first.X);
        Assert.Equal(300, first.Y);
        Assert.Equal(1, first.ClickCount);

        WaitAction wait = Assert.IsType<WaitAction>(document.Actions[1]);
        Assert.Equal(100, wait.DurationMs);

        MouseClickAction second = Assert.IsType<MouseClickAction>(document.Actions[2]);
        Assert.Equal(MacroMouseButton.Right, second.Button);
        Assert.Equal(MacroCoordinateMode.Cursor, second.Mode);
        Assert.Null(second.X);
        Assert.Null(second.Y);

        MacroReadResult again = MacroJson.Read(MacroJson.Write(document));
        Assert.True(again.IsValid);
        MouseClickAction written = Assert.IsType<MouseClickAction>(again.Document!.Actions[2]);
        Assert.Equal(MacroCoordinateMode.Cursor, written.Mode);
        Assert.Null(written.X);
    }

    [Fact]
    public void Unknown_members_survive_a_round_trip()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "keep",
              "name": "Keep",
              "editor": "list",
              "actions": [
                { "id": "note", "type": "comment", "text": "hi", "note": { "keep": true } }
              ]
            }
            """;

        MacroDocument document = MacroJson.Read(json).Document!;
        MacroDocument again = MacroJson.Read(MacroJson.Write(document)).Document!;

        Assert.Equal("list", again.Extras.Single(static extra => extra.Name == "editor").Value.GetValue<string>());
        CommentAction comment = Assert.IsType<CommentAction>(again.Actions[0]);
        Assert.True(comment.Extras.Single(static extra => extra.Name == "note").Value["keep"]!.GetValue<bool>());
    }

    [Fact]
    public void A_move_a_hold_a_wheel_a_key_and_a_repeat_are_accepted()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "gestures",
              "name": "Gestures",
              "actions": [
                { "id": "move", "type": "mouse.move", "coordinateMode": "relative", "x": -4, "y": 6 },
                { "id": "hold", "type": "mouse.hold", "button": "left", "state": "down" },
                { "id": "wheel", "type": "mouse.wheel", "notches": -100 },
                { "id": "key", "type": "keyboard.key", "key": "A", "state": "up" },
                { "id": "loop", "type": "repeat.count", "count": 1000000, "actions": [] }
              ]
            }
            """;

        MacroDocument document = MacroJson.Read(json).Document!;

        MouseMoveAction move = Assert.IsType<MouseMoveAction>(document.Actions[0]);
        Assert.Equal(MacroCoordinateMode.Relative, move.Mode);
        Assert.Equal(-4, move.X);
        MouseHoldAction hold = Assert.IsType<MouseHoldAction>(document.Actions[1]);
        Assert.Equal(MacroPressState.Down, hold.State);
        Assert.Null(hold.X);
        Assert.Equal(-100, Assert.IsType<MouseWheelAction>(document.Actions[2]).Notches);
        Assert.Equal("A", Assert.IsType<KeyboardKeyAction>(document.Actions[3]).Key);
        Assert.Empty(Assert.IsType<RepeatCountAction>(document.Actions[4]).Actions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3600000)]
    public void Wait_accepts_the_closed_range(int duration)
    {
        string json = $$"""
            { "formatVersion": 1, "id": "w", "name": "W", "actions": [
              { "id": "pause", "type": "wait", "durationMs": {{duration}} }
            ] }
            """;

        WaitAction wait = Assert.IsType<WaitAction>(MacroJson.Read(json).Document!.Actions[0]);
        Assert.Equal(duration, wait.DurationMs);
    }

    [Fact]
    public void Eight_repeat_levels_are_accepted_and_a_ninth_is_not()
    {
        Assert.True(MacroJson.Read(Nest(8)).IsValid);
        MacroReadResult ninth = MacroJson.Read(Nest(9));
        Assert.False(ninth.IsValid);
        Assert.Contains(ninth.Issues, static issue => issue.Message.Contains("8 levels", StringComparison.Ordinal));
    }

    [Fact]
    public void Another_format_version_is_rejected_without_reading_actions()
    {
        const string json = """
            { "formatVersion": 2, "id": "x", "name": "X", "actions": [ { "type": "nope" } ] }
            """;

        MacroReadResult result = MacroJson.Read(json);

        Assert.False(result.IsValid);
        MacroIssue issue = Assert.Single(result.Issues);
        Assert.Equal("formatVersion", issue.Path);
    }

    [Theory]
    [InlineData("""{ "formatVersion": "1", "id": "x", "name": "X", "actions": [] }""", "formatVersion")]
    [InlineData("""{ "id": "x", "name": "X", "actions": [] }""", "formatVersion")]
    [InlineData("""[]""", "document")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": " X", "actions": [] }""", "name")]
    [InlineData("""{ "formatVersion": 1, "id": "has space", "name": "X", "actions": [] }""", "id")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X" }""", "actions")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "pluginDependencies": [ { "id": "p" } ], "actions": [] }""", "pluginDependencies")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "mouse.click", "button": "left", "x": 1.5, "y": 2 } ] }""", "actions[0].x")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "mouse.click", "button": "left" } ] }""", "actions[0].x")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "mouse.click", "button": "left", "coordinateMode": "cursor", "x": 1, "y": 2 } ] }""", "actions[0].x")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "mouse.move", "coordinateMode": "cursor", "x": 1, "y": 2 } ] }""", "actions[0].coordinateMode")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "mouse.wheel", "notches": 0 } ] }""", "actions[0].notches")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "keyboard.key", "key": "a", "state": "down" } ] }""", "actions[0].key")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "wait", "durationMs": 3600001 } ] }""", "actions[0].durationMs")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "repeat.until" } ] }""", "actions[0].type")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "branch.if" } ] }""", "actions[0].type")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "plugin.demo" } ] }""", "actions[0].type")]
    [InlineData("""{ "formatVersion": 1, "id": "x", "name": "X", "actions": [ { "id": "a", "type": "comment", "text": "one" }, { "id": "a", "type": "comment", "text": "two" } ] }""", "actions[1].id")]
    public void Invalid_documents_name_the_field(string json, string path)
    {
        MacroReadResult result = MacroJson.Read(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Path == path);
    }

    [Fact]
    public void Variables_must_match_their_type_and_be_unique()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "vars",
              "name": "Vars",
              "variables": [
                { "name": "Count", "type": "number", "default": 1.5 },
                { "name": "Count", "type": "text", "default": "nope" },
                { "name": "1bad", "type": "boolean", "default": "yes" }
              ],
              "actions": []
            }
            """;

        MacroReadResult result = MacroJson.Read(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, static issue => issue.Path == "variables[1].name");
        Assert.Contains(result.Issues, static issue => issue.Path == "variables[2].name");
        Assert.Contains(result.Issues, static issue => issue.Path == "variables[2].default");
    }

    [Fact]
    public void A_utc_timestamp_is_kept_and_a_zoned_one_is_rejected()
    {
        const string valid = """
            {
              "formatVersion": 1,
              "id": "when",
              "name": "When",
              "metadata": { "createdUtc": "2026-11-02T20:00:00Z", "tool": "hand" },
              "actions": []
            }
            """;

        MacroDocument document = MacroJson.Read(valid).Document!;
        Assert.Equal("2026-11-02T20:00:00Z", document.Metadata!.CreatedUtc);
        MacroDocument again = MacroJson.Read(MacroJson.Write(document)).Document!;
        Assert.Equal("hand", again.Metadata!.Extras.Single().Value.GetValue<string>());

        const string zoned = """
            { "formatVersion": 1, "id": "when", "name": "When", "metadata": { "createdUtc": "2026-11-02T20:00:00+01:00" }, "actions": [] }
            """;
        MacroReadResult rejected = MacroJson.Read(zoned);
        Assert.Contains(rejected.Issues, static issue => issue.Path == "metadata.createdUtc");
    }

    [Fact]
    public void A_document_over_2_mib_is_rejected_before_parsing()
    {
        string json = new string('x', MacroFormat.MaxDocumentBytes + 1);

        MacroReadResult result = MacroJson.Read(json);

        MacroIssue issue = Assert.Single(result.Issues);
        Assert.Equal("document", issue.Path);
        Assert.Contains("2 MiB", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_json_properties_are_rejected()
    {
        const string json = """
            { "formatVersion": 1, "formatVersion": 1, "id": "x", "name": "X", "actions": [] }
            """;

        MacroReadResult result = MacroJson.Read(json);

        Assert.False(result.IsValid);
        Assert.Contains("not valid JSON", result.Issues[0].Message, StringComparison.Ordinal);
    }

    private static string Nest(int wraps)
    {
        string inner = """{"id":"leaf","type":"comment","text":"ok"}""";
        for (int level = 1; level <= wraps; level++)
        {
            inner = "{\"id\":\"r" + level + "\",\"type\":\"repeat.count\",\"count\":2,\"actions\":[" + inner + "]}";
        }

        return "{\"formatVersion\":1,\"id\":\"nest\",\"name\":\"Nest\",\"actions\":[" + inner + "]}";
    }
}
