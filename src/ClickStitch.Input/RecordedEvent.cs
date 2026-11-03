namespace ClickStitch.Input;

internal enum RecordedKind
{
    Button,
    Key,
    Move,
    Wheel,
}

/// <summary>
/// One sample the hook accepted. The callback builds this and returns.
/// </summary>
internal readonly struct RecordedEvent
{
    internal RecordedEvent(
        RecordedKind kind,
        long timestamp,
        InputKey key,
        MouseButton button,
        bool down,
        int x,
        int y,
        int wheelDelta)
    {
        Kind = kind;
        Timestamp = timestamp;
        Key = key;
        Button = button;
        Down = down;
        X = x;
        Y = y;
        WheelDelta = wheelDelta;
    }

    internal RecordedKind Kind { get; }

    /// <summary>Monotonic timestamp from <see cref="MonotonicClock.Timestamp"/>.</summary>
    internal long Timestamp { get; }

    internal bool IsKey => Kind == RecordedKind.Key;

    internal InputKey Key { get; }

    internal MouseButton Button { get; }

    internal bool Down { get; }

    /// <summary>Virtual-screen pixels for a move. Other kinds leave this at 0.</summary>
    internal int X { get; }

    /// <summary>Virtual-screen pixels for a move. Other kinds leave this at 0.</summary>
    internal int Y { get; }

    /// <summary>Signed Windows wheel units. One notch is 120. Other kinds leave this at 0.</summary>
    internal int WheelDelta { get; }

    /// <summary>Whole notches toward the user or away. A partial notch is 0 and the raw delta is still kept.</summary>
    internal int Notches => WheelDelta / 120;

    internal static RecordedEvent Mouse(long timestamp, MouseButton button, bool down, int x = 0, int y = 0) =>
        new(RecordedKind.Button, timestamp, default, button, down, x, y, 0);

    internal static RecordedEvent KeyPress(long timestamp, InputKey key, bool down) =>
        new(RecordedKind.Key, timestamp, key, default, down, 0, 0, 0);

    internal static RecordedEvent Move(long timestamp, int x, int y) =>
        new(RecordedKind.Move, timestamp, default, default, false, x, y, 0);

    internal static RecordedEvent Wheel(long timestamp, int wheelDelta) =>
        new(RecordedKind.Wheel, timestamp, default, default, false, 0, 0, wheelDelta);
}

/// <summary>
/// Which samples a recording accepts. Movement is off unless the caller asks for it.
/// </summary>
internal readonly record struct RecordingOptions
{
    internal static RecordingOptions Default { get; } = new()
    {
        CaptureWheel = true,
        IgnoreInjected = true,
        IncludeModifiers = true,
    };

    internal bool CaptureMovement { get; init; }

    internal bool CaptureWheel { get; init; }

    internal bool IgnoreInjected { get; init; }

    /// <summary>Drop a move closer in time than this to the previous kept move. 0 keeps every move.</summary>
    internal int SampleMilliseconds { get; init; }

    /// <summary>Drop a move closer than this many pixels to the previous kept move. 0 keeps every move.</summary>
    internal int MinimumDistance { get; init; }

    /// <summary>Drop a move within this many pixels of the straight line between its neighbors. 0 keeps every move.</summary>
    internal int Tolerance { get; init; }

    /// <summary>When false, Shift, Control, Alt, and the Windows keys are left out of the macro.</summary>
    internal bool IncludeModifiers { get; init; }
}

/// <summary>
/// The fields the hook callback copies before it returns.
/// </summary>
internal readonly struct HookSample
{
    internal HookSample(
        int message,
        uint virtualKey,
        bool injected,
        long timestamp,
        int x = 0,
        int y = 0,
        int wheelDelta = 0)
    {
        Message = message;
        VirtualKey = virtualKey;
        Injected = injected;
        Timestamp = timestamp;
        X = x;
        Y = y;
        WheelDelta = wheelDelta;
    }

    internal int Message { get; }

    internal uint VirtualKey { get; }

    internal bool Injected { get; }

    internal long Timestamp { get; }

    internal int X { get; }

    internal int Y { get; }

    internal int WheelDelta { get; }
}
