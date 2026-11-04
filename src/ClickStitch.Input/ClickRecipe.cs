using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// Builds one format-1 document for the front-page clicker. It does not send input.
/// Hold stays down for up to an hour; Stop ends the run and releases what is held.
/// </summary>
public static class ClickRecipe
{
    public const int UntilStopped = 1_000_000;

    public const int HoldLimitMilliseconds = 3_600_000;

    public static bool TryBuild(
        string target,
        bool hold,
        bool atCursor,
        int x,
        int y,
        int clickCount,
        int times,
        int gapMilliseconds,
        out string json,
        out string problem)
    {
        json = "";
        if (!TryTarget(target, out bool mouse, out string token, out problem))
        {
            return false;
        }

        if (times is < 1 or > UntilStopped)
        {
            problem = "Count must be a whole number from 1 through 1000000.";
            return false;
        }

        if (gapMilliseconds is < 0 or > HoldLimitMilliseconds)
        {
            problem = "The gap must be a whole number from 0 through 3600000 milliseconds.";
            return false;
        }

        if (clickCount is < 1 or > 3)
        {
            problem = "Clicks must be 1, 2, or 3.";
            return false;
        }

        if (!atCursor && (x is < -32768 or > 32767 || y is < -32768 or > 32767))
        {
            problem = "X and Y must be whole numbers from -32768 through 32767.";
            return false;
        }

        string actions = hold
            ? Hold(mouse, token, atCursor, x, y)
            : Spam(mouse, token, atCursor, x, y, clickCount, times, gapMilliseconds);
        json = "{ \"formatVersion\": 1, \"id\": \"click\", \"name\": \"Click\", \"actions\": [ " + actions + " ] }";
        return true;
    }

    private static bool TryTarget(string target, out bool mouse, out string token, out string problem)
    {
        mouse = false;
        token = "";
        problem = "";
        if (target is "left" or "right" or "middle")
        {
            mouse = true;
            token = target;
            return true;
        }

        if (MacroKeys.IsKnown(target))
        {
            token = target;
            return true;
        }

        problem = "Choose a mouse button or a key.";
        return false;
    }

    private static string Hold(bool mouse, string token, bool atCursor, int x, int y)
    {
        string down = mouse
            ? "{ \"id\": \"down\", \"type\": \"mouse.hold\", \"button\": \"" + token + "\", \"state\": \"down\"" + Place(atCursor, x, y) + " }"
            : "{ \"id\": \"down\", \"type\": \"keyboard.key\", \"key\": \"" + token + "\", \"state\": \"down\" }";
        return down + ", { \"id\": \"stay\", \"type\": \"wait\", \"durationMs\": 3600000 }";
    }

    private static string Spam(bool mouse, string token, bool atCursor, int x, int y, int clickCount, int times, int gapMilliseconds)
    {
        string step = mouse
            ? "{ \"id\": \"click\", \"type\": \"mouse.click\", \"button\": \"" + token + "\", \"clickCount\": " + clickCount + Place(atCursor, x, y) + " }"
            : "{ \"id\": \"down\", \"type\": \"keyboard.key\", \"key\": \"" + token + "\", \"state\": \"down\" }, { \"id\": \"up\", \"type\": \"keyboard.key\", \"key\": \"" + token + "\", \"state\": \"up\" }";
        return "{ \"id\": \"again\", \"type\": \"repeat.count\", \"count\": " + times
            + ", \"actions\": [ " + step + ", { \"id\": \"gap\", \"type\": \"wait\", \"durationMs\": " + gapMilliseconds + " } ] }";
    }

    private static string Place(bool atCursor, int x, int y) =>
        atCursor ? ", \"coordinateMode\": \"cursor\"" : ", \"x\": " + x + ", \"y\": " + y;
}
