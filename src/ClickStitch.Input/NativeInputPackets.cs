using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Native <c>MOUSEINPUT</c>. On x64, <see cref="dwExtraInfo"/> is at byte 24 and the struct is 32 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeMouseInput
{
    internal int dx;
    internal int dy;
    internal uint mouseData;
    internal uint dwFlags;
    internal uint time;
    internal nuint dwExtraInfo;
}

/// <summary>
/// Native <c>KEYBDINPUT</c>. On x64, <see cref="dwExtraInfo"/> is at byte 16 and the struct is 24 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeKeyboardInput
{
    internal ushort wVk;
    internal ushort wScan;
    internal uint dwFlags;
    internal uint time;
    internal nuint dwExtraInfo;
}

/// <summary>
/// Native <c>HARDWAREINPUT</c>. Present so the input union matches Win32. ClickStitch does not submit these packets.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeHardwareInput
{
    internal uint uMsg;
    internal ushort wParamL;
    internal ushort wParamH;
}

/// <summary>
/// Native union inside <c>INPUT</c>. All three views start at offset 0.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct NativeInputUnion
{
    [FieldOffset(0)]
    internal NativeMouseInput Mouse;

    [FieldOffset(0)]
    internal NativeKeyboardInput Keyboard;

    [FieldOffset(0)]
    internal NativeHardwareInput Hardware;
}

/// <summary>
/// Native <c>INPUT</c>. On x64 the struct is 40 bytes and <see cref="Data"/> starts at byte 8.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeInput
{
    internal uint type;
    internal NativeInputUnion Data;
}
