using System.Runtime.InteropServices;

namespace ClickStitch.Input;

internal static class NativeMethods
{
    /// <summary>
    /// Inserts the events in <paramref name="inputs"/>. The returned count can be shorter than the array.
    /// Call <see cref="Marshal.GetLastWin32Error"/> immediately after this method when the count is short.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SendInput")]
    internal static extern uint SendInput(uint count, NativeInput[] inputs, int structureSize);
}
