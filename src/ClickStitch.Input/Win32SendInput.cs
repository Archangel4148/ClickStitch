using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Calls <c>SendInput</c> and reads the Win32 error before any other work.
/// </summary>
internal sealed class Win32SendInput : INativeSendInput
{
    public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
    {
        uint accepted = NativeMethods.SendInput((uint)inputs.Length, inputs, structureSize);
        int win32Error = Marshal.GetLastWin32Error();
        return (accepted, win32Error);
    }
}
