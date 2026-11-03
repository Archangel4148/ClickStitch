using System.Runtime.InteropServices;

namespace ClickStitch.Input;

/// <summary>
/// Submits one batch of native input packets. It does not track which buttons are held.
/// </summary>
internal sealed class SendInputBatch
{
    private readonly INativeSendInput _native;

    internal SendInputBatch(INativeSendInput native)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
    }

    internal static SendInputBatch CreateForWindows() => new(new Win32SendInput());

    internal InputSubmission Submit(NativeInput[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Length == 0)
        {
            return InputSubmission.Empty;
        }

        for (int index = 0; index < inputs.Length; index++)
        {
            uint type = inputs[index].type;
            if (type != NativeInputConstants.InputMouse && type != NativeInputConstants.InputKeyboard)
            {
                throw new ArgumentException(
                    $"Packet {index} has type {type}. Only mouse and keyboard packets can be submitted.",
                    nameof(inputs));
            }
        }

        int structureSize = Marshal.SizeOf<NativeInput>();
        (uint accepted, int win32Error) = _native.Send(inputs, structureSize);
        return InputSubmission.Interpret(inputs.Length, accepted, win32Error);
    }
}
