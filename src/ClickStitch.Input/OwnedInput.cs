namespace ClickStitch.Input;

internal enum OwnedDevice
{
    Mouse,
    Keyboard,
}

/// <summary>
/// One simulated button or key the engine has pressed and has not yet released.
/// Physical keys the user is holding are never represented here.
/// </summary>
internal readonly record struct OwnedInput(OwnedDevice Device, int Code) : IComparable<OwnedInput>
{
    internal static OwnedInput Mouse(MouseButton button) => new(OwnedDevice.Mouse, (int)button);

    internal static OwnedInput Key(InputKey key) => new(OwnedDevice.Keyboard, (int)key);

    internal MouseButton MouseButton => (MouseButton)Code;

    internal InputKey KeyboardKey => (InputKey)Code;

    public int CompareTo(OwnedInput other)
    {
        int device = Device.CompareTo(other.Device);
        if (device != 0)
        {
            return device;
        }

        return Code.CompareTo(other.Code);
    }
}
