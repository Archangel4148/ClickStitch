namespace ClickStitch.Input;

internal enum OwnershipChange
{
    None,
    Press,
    Release,
}

/// <summary>
/// One native packet and the ownership change it causes if Windows accepts it.
/// </summary>
internal readonly struct PlannedEvent
{
    internal PlannedEvent(NativeInput packet, OwnershipChange change, OwnedInput input)
    {
        Packet = packet;
        Change = change;
        Input = input;
    }

    internal NativeInput Packet { get; }

    internal OwnershipChange Change { get; }

    internal OwnedInput Input { get; }
}

internal readonly struct GesturePlan
{
    internal GesturePlan(PlannedEvent[] events)
    {
        Events = events;
    }

    internal PlannedEvent[] Events { get; }

    internal NativeInput[] Packets()
    {
        var packets = new NativeInput[Events.Length];
        for (int index = 0; index < Events.Length; index++)
        {
            packets[index] = Events[index].Packet;
        }

        return packets;
    }
}

internal readonly struct GesturePreparation
{
    private GesturePreparation(bool outsideScreen, GesturePlan plan)
    {
        OutsideScreen = outsideScreen;
        Plan = plan;
    }

    internal bool OutsideScreen { get; }

    internal GesturePlan Plan { get; }

    internal static GesturePreparation Outside { get; } = new(true, new GesturePlan([]));

    internal static GesturePreparation Ready(params PlannedEvent[] events) => new(false, new GesturePlan(events));
}

/// <summary>
/// Builds gesture packets without submitting them.
/// </summary>
internal static class GesturePlanner
{
    internal static GesturePreparation MoveTo(VirtualScreen screen, int x, int y)
    {
        if (!PointerCoordinates.TryToAbsolute(screen, x, y, out int absoluteX, out int absoluteY))
        {
            return GesturePreparation.Outside;
        }

        return GesturePreparation.Ready(Unaffected(Mouse(absoluteX, absoluteY, NativeInputConstants.MouseMoveAbsoluteVirtualDesk)));
    }

    internal static GesturePreparation MoveBy(int dx, int dy)
    {
        (int relativeX, int relativeY) = PointerCoordinates.Relative(dx, dy);
        return GesturePreparation.Ready(Unaffected(Mouse(relativeX, relativeY, NativeInputConstants.MouseEventMove)));
    }

    internal static GesturePreparation MouseDown(MouseButton button)
    {
        (uint down, _) = ButtonFlags(button);
        return GesturePreparation.Ready(Press(Mouse(0, 0, down), OwnedInput.Mouse(button)));
    }

    internal static GesturePreparation MouseUp(MouseButton button)
    {
        (_, uint up) = ButtonFlags(button);
        return GesturePreparation.Ready(Release(Mouse(0, 0, up), OwnedInput.Mouse(button)));
    }

    internal static GesturePreparation MouseDownAt(VirtualScreen screen, int x, int y, MouseButton button)
    {
        return ButtonAt(screen, x, y, button, down: true);
    }

    internal static GesturePreparation MouseUpAt(VirtualScreen screen, int x, int y, MouseButton button)
    {
        return ButtonAt(screen, x, y, button, down: false);
    }

    internal static GesturePreparation Click(MouseButton button, int count)
    {
        RequireCount(count);
        return GesturePreparation.Ready(ClickEvents(button, count, move: null));
    }

    internal static GesturePreparation ClickAt(VirtualScreen screen, int x, int y, MouseButton button, int count)
    {
        RequireCount(count);
        if (!PointerCoordinates.TryToAbsolute(screen, x, y, out int absoluteX, out int absoluteY))
        {
            return GesturePreparation.Outside;
        }

        PlannedEvent move = Unaffected(Mouse(absoluteX, absoluteY, NativeInputConstants.MouseMoveAbsoluteVirtualDesk));
        return GesturePreparation.Ready(ClickEvents(button, count, move));
    }

    internal static GesturePreparation Wheel(int notches)
    {
        if (notches is 0 or < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(notches), "Wheel notches must be from -100 through 100, excluding 0.");
        }

        uint mouseData = unchecked((uint)(notches * NativeInputConstants.WheelDelta));
        return GesturePreparation.Ready(Unaffected(Mouse(0, 0, NativeInputConstants.MouseEventWheel, mouseData)));
    }

    internal static GesturePreparation KeyDown(InputKey key)
    {
        return GesturePreparation.Ready(Press(Key(key, up: false), OwnedInput.Key(key)));
    }

    internal static GesturePreparation KeyUp(InputKey key)
    {
        return GesturePreparation.Ready(Release(Key(key, up: true), OwnedInput.Key(key)));
    }

    private static GesturePreparation ButtonAt(VirtualScreen screen, int x, int y, MouseButton button, bool down)
    {
        if (!PointerCoordinates.TryToAbsolute(screen, x, y, out int absoluteX, out int absoluteY))
        {
            return GesturePreparation.Outside;
        }

        (uint downFlag, uint upFlag) = ButtonFlags(button);
        PlannedEvent move = Unaffected(Mouse(absoluteX, absoluteY, NativeInputConstants.MouseMoveAbsoluteVirtualDesk));
        PlannedEvent buttonEvent = down
            ? Press(Mouse(0, 0, downFlag), OwnedInput.Mouse(button))
            : Release(Mouse(0, 0, upFlag), OwnedInput.Mouse(button));
        return GesturePreparation.Ready(move, buttonEvent);
    }

    private static PlannedEvent[] ClickEvents(MouseButton button, int count, PlannedEvent? move)
    {
        (uint down, uint up) = ButtonFlags(button);
        OwnedInput input = OwnedInput.Mouse(button);
        int offset = move.HasValue ? 1 : 0;
        var events = new PlannedEvent[offset + (count * 2)];
        if (move.HasValue)
        {
            events[0] = move.Value;
        }

        for (int index = 0; index < count; index++)
        {
            events[offset + (index * 2)] = Press(Mouse(0, 0, down), input);
            events[offset + (index * 2) + 1] = Release(Mouse(0, 0, up), input);
        }

        return events;
    }

    private static (uint Down, uint Up) ButtonFlags(MouseButton button)
    {
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "The mouse button is not in the macro schema.");
        }

        return button switch
        {
            MouseButton.Left => (NativeInputConstants.MouseEventLeftDown, NativeInputConstants.MouseEventLeftUp),
            MouseButton.Right => (NativeInputConstants.MouseEventRightDown, NativeInputConstants.MouseEventRightUp),
            MouseButton.Middle => (NativeInputConstants.MouseEventMiddleDown, NativeInputConstants.MouseEventMiddleUp),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "The mouse button is not in the macro schema."),
        };
    }

    private static void RequireCount(int count)
    {
        if (count is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Click count must be from 1 through 3.");
        }
    }

    private static PlannedEvent Unaffected(NativeInput packet) => new(packet, OwnershipChange.None, default);

    private static PlannedEvent Press(NativeInput packet, OwnedInput input) => new(packet, OwnershipChange.Press, input);

    private static PlannedEvent Release(NativeInput packet, OwnedInput input) => new(packet, OwnershipChange.Release, input);

    private static NativeInput Mouse(int dx, int dy, uint flags, uint mouseData = 0)
    {
        return new NativeInput
        {
            type = NativeInputConstants.InputMouse,
            Data = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    dx = dx,
                    dy = dy,
                    mouseData = mouseData,
                    dwFlags = flags,
                },
            },
        };
    }

    private static NativeInput Key(InputKey key, bool up)
    {
        KeyStroke stroke = InputKeys.Describe(key);
        uint flags = 0;
        if (stroke.Extended)
        {
            flags |= NativeInputConstants.KeyEventExtendedKey;
        }

        if (up)
        {
            flags |= NativeInputConstants.KeyEventKeyUp;
        }

        return new NativeInput
        {
            type = NativeInputConstants.InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    wVk = stroke.VirtualKey,
                    dwFlags = flags,
                },
            },
        };
    }
}
