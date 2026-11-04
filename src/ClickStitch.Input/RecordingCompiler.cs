using System.Text.Json;
using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// A format-1 document built from captured samples, plus notes about samples that were left out.
/// </summary>
internal sealed class RecordedDocument
{
    internal RecordedDocument(MacroDocument? document, IReadOnlyList<string> notices)
    {
        Document = document;
        Notices = notices;
    }

    internal MacroDocument? Document { get; }

    internal IReadOnlyList<string> Notices { get; }

    internal bool IsReady => Document is not null;
}

/// <summary>
/// Turns captured samples into a format-1 macro. The raw list is not changed.
/// A down immediately followed by the same button's up becomes one click.
/// The time until that release stays in the gap before the next action, so playback keeps the original length.
/// Anything between them becomes a hold. Waits come from the monotonic timestamps.
/// </summary>
internal static class RecordingCompiler
{
    private const int MaximumWait = 3_600_000;
    private const int MinimumPoint = -32768;
    private const int MaximumPoint = 32767;

    internal static RecordedDocument Compile(
        IReadOnlyList<RecordedEvent> events,
        string name,
        string id,
        bool includeModifiers = true)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(id);

        var drafts = new List<Draft>();
        var notices = new List<string>();
        var heldButtons = new HashSet<MouseButton>();
        var heldKeys = new HashSet<InputKey>();
        int wheelRemainder = 0;
        int skippedMoves = 0;
        bool emittedMove = false;
        bool emittedButton = false;
        bool started = false;
        long lastStamp = 0;

        for (int index = 0; index < events.Count; index++)
        {
            RecordedEvent current = events[index];
            if (current.Kind == RecordedKind.Button && current.Down)
            {
                int match = FindUp(events, index);
                if (match == index + 1)
                {
                    Place(drafts, ref started, ref lastStamp, current.Timestamp, () =>
                    {
                        drafts.Add(Draft.Click(current.Button));
                        emittedButton = true;
                    });
                    index = match;
                    continue;
                }
            }

            switch (current.Kind)
            {
                case RecordedKind.Move:
                    if (current.X < MinimumPoint || current.X > MaximumPoint || current.Y < MinimumPoint || current.Y > MaximumPoint)
                    {
                        skippedMoves++;
                        Touch(ref started, ref lastStamp, current.Timestamp);
                        break;
                    }

                    Place(drafts, ref started, ref lastStamp, current.Timestamp, () =>
                    {
                        drafts.Add(Draft.Move(current.X, current.Y));
                        emittedMove = true;
                    });
                    break;
                case RecordedKind.Wheel:
                    wheelRemainder += current.WheelDelta;
                    if (Math.Abs(wheelRemainder) < 120)
                    {
                        break;
                    }

                    int remainder = wheelRemainder;
                    Place(drafts, ref started, ref lastStamp, current.Timestamp, () =>
                    {
                        while (Math.Abs(remainder) >= 120)
                        {
                            int notches = remainder / 120;
                            if (notches > 100)
                            {
                                notches = 100;
                            }
                            else if (notches < -100)
                            {
                                notches = -100;
                            }

                            drafts.Add(Draft.Wheel(notches));
                            remainder -= notches * 120;
                        }
                    });
                    wheelRemainder = remainder;
                    break;
                case RecordedKind.Key:
                    if (!includeModifiers && IsModifier(current.Key))
                    {
                        break;
                    }

                    if (current.Down)
                    {
                        if (!heldKeys.Add(current.Key))
                        {
                            break;
                        }

                        Place(drafts, ref started, ref lastStamp, current.Timestamp, () => drafts.Add(Draft.KeyPress(current.Key, true)));
                    }
                    else if (!heldKeys.Remove(current.Key))
                    {
                        notices.Add("A key release with no recorded press was left out.");
                    }
                    else
                    {
                        Place(drafts, ref started, ref lastStamp, current.Timestamp, () => drafts.Add(Draft.KeyPress(current.Key, false)));
                    }

                    break;
                case RecordedKind.Button:
                    if (current.Down)
                    {
                        if (!heldButtons.Add(current.Button))
                        {
                            break;
                        }

                        Place(drafts, ref started, ref lastStamp, current.Timestamp, () =>
                        {
                            drafts.Add(Draft.Hold(current.Button, true));
                            emittedButton = true;
                        });
                    }
                    else if (!heldButtons.Remove(current.Button))
                    {
                        notices.Add("A button release with no recorded press was left out.");
                    }
                    else
                    {
                        Place(drafts, ref started, ref lastStamp, current.Timestamp, () =>
                        {
                            drafts.Add(Draft.Hold(current.Button, false));
                            emittedButton = true;
                        });
                    }

                    break;
            }
        }

        if (skippedMoves > 0)
        {
            notices.Add(skippedMoves == 1
                ? "1 move was outside the schema range and was left out."
                : skippedMoves + " moves were outside the schema range and were left out.");
        }

        if (wheelRemainder != 0)
        {
            notices.Add("A partial wheel movement of " + wheelRemainder + " units was left out.");
        }

        if (emittedButton && !emittedMove)
        {
            notices.Add("Buttons were recorded without a pointer position. Playback uses the cursor.");
        }

        MacroReadResult read = MacroJson.Read(WriteJson(name, id, drafts));
        if (!read.IsValid || read.Document is null)
        {
            foreach (MacroIssue issue in read.Issues)
            {
                notices.Add(issue.Path + ": " + issue.Message);
            }

            return new RecordedDocument(null, notices);
        }

        return new RecordedDocument(read.Document, notices);
    }

    private static int FindUp(IReadOnlyList<RecordedEvent> events, int downIndex)
    {
        MouseButton button = events[downIndex].Button;
        for (int index = downIndex + 1; index < events.Count; index++)
        {
            RecordedEvent candidate = events[index];
            if (candidate.Kind != RecordedKind.Button || candidate.Button != button)
            {
                continue;
            }

            return candidate.Down ? -1 : index;
        }

        return -1;
    }

    private static void Place(List<Draft> drafts, ref bool started, ref long lastStamp, long timestamp, Action emit)
    {
        if (started)
        {
            AddWaits(drafts, RecordingTime.ElapsedMilliseconds(lastStamp, timestamp));
        }

        emit();
        lastStamp = timestamp;
        started = true;
    }

    private static void Touch(ref bool started, ref long lastStamp, long timestamp)
    {
        lastStamp = timestamp;
        started = true;
    }

    private static void AddWaits(List<Draft> drafts, int milliseconds)
    {
        while (milliseconds > 0)
        {
            int slice = Math.Min(milliseconds, MaximumWait);
            drafts.Add(Draft.Wait(slice));
            milliseconds -= slice;
        }
    }

    private static bool IsModifier(InputKey key) =>
        key is InputKey.Shift or InputKey.Control or InputKey.Alt or InputKey.LeftWindows or InputKey.RightWindows;

    private static string WriteJson(string name, string id, List<Draft> drafts)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", MacroFormat.CurrentVersion);
            writer.WriteString("id", id);
            writer.WriteString("name", name);
            writer.WriteString("description", "Recorded from the pointer and the keyboard.");
            writer.WriteStartObject("coordinateSpace");
            writer.WriteString("system", "virtualDesktopPixels");
            writer.WriteEndObject();
            writer.WriteStartObject("settings");
            writer.WriteBoolean("stopOnError", true);
            writer.WriteEndObject();
            writer.WriteStartArray("actions");
            for (int index = 0; index < drafts.Count; index++)
            {
                drafts[index].Write(writer, "a" + (index + 1));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private readonly struct Draft
    {
        private Draft(string type, string button, string state, string key, int x, int y, int amount)
        {
            Type = type;
            Button = button;
            State = state;
            Key = key;
            X = x;
            Y = y;
            Amount = amount;
        }

        private string Type { get; }

        private string Button { get; }

        private string State { get; }

        private string Key { get; }

        private int X { get; }

        private int Y { get; }

        private int Amount { get; }

        internal static Draft Move(int x, int y) => new("mouse.move", "", "", "", x, y, 0);

        internal static Draft Click(MouseButton button) => new("mouse.click", Name(button), "", "", 0, 0, 1);

        internal static Draft Hold(MouseButton button, bool down) =>
            new("mouse.hold", Name(button), down ? "down" : "up", "", 0, 0, 0);

        internal static Draft Wheel(int notches) => new("mouse.wheel", "", "", "", 0, 0, notches);

        internal static Draft KeyPress(InputKey key, bool down) => new("keyboard.key", "", down ? "down" : "up", key.ToString(), 0, 0, 0);

        internal static Draft Wait(int milliseconds) => new("wait", "", "", "", 0, 0, milliseconds);

        internal void Write(Utf8JsonWriter writer, string id)
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("type", Type);
            switch (Type)
            {
                case "mouse.move":
                    writer.WriteString("coordinateMode", "virtualDesktop");
                    writer.WriteNumber("x", X);
                    writer.WriteNumber("y", Y);
                    break;
                case "mouse.click":
                    writer.WriteString("button", Button);
                    writer.WriteString("coordinateMode", "cursor");
                    writer.WriteNumber("clickCount", Amount);
                    break;
                case "mouse.hold":
                    writer.WriteString("button", Button);
                    writer.WriteString("state", State);
                    break;
                case "mouse.wheel":
                    writer.WriteNumber("notches", Amount);
                    break;
                case "keyboard.key":
                    writer.WriteString("key", Key);
                    writer.WriteString("state", State);
                    break;
                case "wait":
                    writer.WriteNumber("durationMs", Amount);
                    break;
            }

            writer.WriteEndObject();
        }

        private static string Name(MouseButton button) => button switch
        {
            MouseButton.Right => "right",
            MouseButton.Middle => "middle",
            _ => "left",
        };
    }
}
