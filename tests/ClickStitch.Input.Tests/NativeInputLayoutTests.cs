using System.Runtime.InteropServices;

namespace ClickStitch.Input.Tests;

public sealed class NativeInputLayoutTests
{
    [Fact]
    public void Process_is_64_bit()
    {
        Assert.True(Environment.Is64BitProcess);
    }

    [Fact]
    public void Packet_sizes_match_the_x64_Win32_layout()
    {
        Assert.Equal(32, Marshal.SizeOf<NativeMouseInput>());
        Assert.Equal(24, Marshal.SizeOf<NativeKeyboardInput>());
        Assert.Equal(8, Marshal.SizeOf<NativeHardwareInput>());
        Assert.Equal(40, Marshal.SizeOf<NativeInput>());
    }

    [Fact]
    public void Extra_info_and_union_offsets_match_the_x64_Win32_layout()
    {
        Assert.Equal(24, Marshal.OffsetOf<NativeMouseInput>(nameof(NativeMouseInput.dwExtraInfo)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeKeyboardInput>(nameof(NativeKeyboardInput.dwExtraInfo)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<NativeInput>(nameof(NativeInput.Data)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<NativeInputUnion>(nameof(NativeInputUnion.Mouse)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<NativeInputUnion>(nameof(NativeInputUnion.Keyboard)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<NativeInputUnion>(nameof(NativeInputUnion.Hardware)).ToInt32());
    }

    [Fact]
    public void Absolute_move_flags_combine_move_absolute_and_virtual_desk()
    {
        Assert.Equal(
            NativeInputConstants.MouseEventMove
                | NativeInputConstants.MouseEventAbsolute
                | NativeInputConstants.MouseEventVirtualDesk,
            NativeInputConstants.MouseMoveAbsoluteVirtualDesk);
        Assert.Equal(120, NativeInputConstants.WheelDelta);
    }
}
