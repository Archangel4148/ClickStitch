namespace ClickStitch.Input;

/// <summary>
/// Win32 values used by <c>SendInput</c>. These match the Windows headers and stay stable.
/// </summary>
internal static class NativeInputConstants
{
    internal const uint InputMouse = 0;
    internal const uint InputKeyboard = 1;
    internal const uint InputHardware = 2;

    internal const uint MouseEventMove = 0x0001;
    internal const uint MouseEventLeftDown = 0x0002;
    internal const uint MouseEventLeftUp = 0x0004;
    internal const uint MouseEventRightDown = 0x0008;
    internal const uint MouseEventRightUp = 0x0010;
    internal const uint MouseEventMiddleDown = 0x0020;
    internal const uint MouseEventMiddleUp = 0x0040;
    internal const uint MouseEventXDown = 0x0080;
    internal const uint MouseEventXUp = 0x0100;
    internal const uint MouseEventWheel = 0x0800;
    internal const uint MouseEventHorizontalWheel = 0x1000;
    internal const uint MouseEventMoveNoCoalesce = 0x2000;
    internal const uint MouseEventVirtualDesk = 0x4000;
    internal const uint MouseEventAbsolute = 0x8000;

    /// <summary>
    /// Flags for a move in normalized virtual-desktop coordinates.
    /// </summary>
    internal const uint MouseMoveAbsoluteVirtualDesk =
        MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk;

    internal const int WheelDelta = 120;
    internal const uint XButton1 = 0x0001;
    internal const uint XButton2 = 0x0002;

    internal const uint KeyEventExtendedKey = 0x0001;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;
    internal const uint KeyEventScanCode = 0x0008;
}
