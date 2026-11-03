namespace ClickStitch.Input;

/// <summary>
/// The native insert operation. Production code calls <c>SendInput</c>. Tests supply a stand-in.
/// </summary>
internal interface INativeSendInput
{
    (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize);
}
