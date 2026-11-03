namespace ClickStitch.Input.Tests;

public sealed class InputPrimitiveTests
{
    private static readonly VirtualScreen Primary = new(0, 0, 1920, 1080);

    [Fact]
    public void Schema_has_the_closed_key_list()
    {
        Assert.Equal(KeyStroke.SchemaKeyCount, Enum.GetValues<InputKey>().Length);
    }

    [Fact]
    public void Every_schema_key_maps_to_a_virtual_key()
    {
        foreach (InputKey key in Enum.GetValues<InputKey>())
        {
            KeyStroke stroke = InputKeys.Describe(key);
            Assert.NotEqual((ushort)0, stroke.VirtualKey);
        }
    }

    [Fact]
    public void Known_keys_use_the_Win32_virtual_key()
    {
        (InputKey Key, int VirtualKey, bool Extended)[] samples =
        [
            (InputKey.A, 0x41, false),
            (InputKey.Z, 0x5A, false),
            (InputKey.Digit0, 0x30, false),
            (InputKey.Digit9, 0x39, false),
            (InputKey.F1, 0x70, false),
            (InputKey.F24, 0x87, false),
            (InputKey.Shift, 0x10, false),
            (InputKey.Control, 0x11, false),
            (InputKey.Alt, 0x12, false),
            (InputKey.Enter, 0x0D, false),
            (InputKey.Backspace, 0x08, false),
            (InputKey.LeftWindows, 0x5B, true),
            (InputKey.RightWindows, 0x5C, true),
            (InputKey.Left, 0x25, true),
            (InputKey.Delete, 0x2E, true),
        ];

        foreach ((InputKey key, int virtualKey, bool extended) in samples)
        {
            KeyStroke stroke = InputKeys.Describe(key);
            Assert.Equal((ushort)virtualKey, stroke.VirtualKey);
            Assert.Equal(extended, stroke.Extended);
        }
    }

    [Fact]
    public void Key_down_and_key_up_are_separate_packets()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult down = primitives.KeyDown(InputKey.Shift);
        PrimitiveResult typed = primitives.KeyDown(InputKey.A);
        PrimitiveResult released = primitives.KeyUp(InputKey.A);
        PrimitiveResult shiftUp = primitives.KeyUp(InputKey.Shift);

        Assert.Equal(4, native.Calls);
        Assert.True(down.Submission.IsFull);
        Assert.True(typed.Submission.IsFull);
        Assert.True(released.Submission.IsFull);
        Assert.True(shiftUp.Submission.IsFull);
        Assert.Equal(0x10, native.Packets[0].Data.Keyboard.wVk);
        Assert.Equal(0u, native.Packets[0].Data.Keyboard.dwFlags);
        Assert.Equal(0x41, native.Packets[1].Data.Keyboard.wVk);
        Assert.Equal(NativeInputConstants.KeyEventKeyUp, native.Packets[2].Data.Keyboard.dwFlags);
        Assert.Equal(
            NativeInputConstants.KeyEventKeyUp,
            native.Packets[3].Data.Keyboard.dwFlags);
        Assert.Equal((ushort)0, native.Packets[1].Data.Keyboard.wScan);
    }

    [Fact]
    public void Extended_key_up_sets_both_flags()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        primitives.KeyUp(InputKey.Left);

        Assert.Equal(
            NativeInputConstants.KeyEventExtendedKey | NativeInputConstants.KeyEventKeyUp,
            native.Packets[0].Data.Keyboard.dwFlags);
        Assert.Equal(0x25, native.Packets[0].Data.Keyboard.wVk);
    }

    [Fact]
    public void An_undefined_key_is_rejected_before_a_native_call()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        Assert.Throws<ArgumentOutOfRangeException>(() => primitives.KeyDown((InputKey)999));
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void Click_sends_down_and_up_in_one_batch()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult result = primitives.Click(MouseButton.Left);

        Assert.Equal(PrimitiveDelivery.Sent, result.Delivery);
        Assert.Equal(1, native.Calls);
        Assert.Equal(2, native.Packets.Count);
        Assert.Equal(NativeInputConstants.MouseEventLeftDown, native.Packets[0].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventLeftUp, native.Packets[1].Data.Mouse.dwFlags);
        Assert.Equal(0, native.Packets[0].Data.Mouse.dx);
        Assert.Equal(0u, native.Packets[0].Data.Mouse.dwExtraInfo);
    }

    [Fact]
    public void Click_at_a_point_moves_before_the_button_pair()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult result = primitives.ClickAt(Primary, 0, 0, MouseButton.Right, 2);

        Assert.Equal(PrimitiveDelivery.Sent, result.Delivery);
        Assert.Equal(1, native.Calls);
        Assert.Equal(5, native.Packets.Count);
        Assert.Equal(NativeInputConstants.MouseMoveAbsoluteVirtualDesk, native.Packets[0].Data.Mouse.dwFlags);
        Assert.Equal(0, native.Packets[0].Data.Mouse.dx);
        Assert.Equal(0, native.Packets[0].Data.Mouse.dy);
        Assert.Equal(NativeInputConstants.MouseEventRightDown, native.Packets[1].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventRightUp, native.Packets[2].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventRightDown, native.Packets[3].Data.Mouse.dwFlags);
        Assert.Equal(NativeInputConstants.MouseEventRightUp, native.Packets[4].Data.Mouse.dwFlags);
    }

    [Fact]
    public void Click_outside_the_screen_sends_nothing()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult result = primitives.ClickAt(Primary, -1, 0, MouseButton.Left);

        Assert.Equal(PrimitiveDelivery.OutsideScreen, result.Delivery);
        Assert.Equal(0, native.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Click_count_outside_1_through_3_is_rejected(int count)
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        Assert.Throws<ArgumentOutOfRangeException>(() => primitives.Click(MouseButton.Left, count));
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void Middle_button_down_at_a_point_is_one_batch()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        primitives.MouseDownAt(Primary, 1919, 1079, MouseButton.Middle);

        Assert.Equal(1, native.Calls);
        Assert.Equal(2, native.Packets.Count);
        Assert.Equal(65535, native.Packets[0].Data.Mouse.dx);
        Assert.Equal(65535, native.Packets[0].Data.Mouse.dy);
        Assert.Equal(NativeInputConstants.MouseEventMiddleDown, native.Packets[1].Data.Mouse.dwFlags);
    }

    [Fact]
    public void Button_up_without_a_move_uses_the_current_cursor()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        primitives.MouseUp(MouseButton.Left);

        NativeInput packet = Assert.Single(native.Packets);
        Assert.Equal(NativeInputConstants.MouseEventLeftUp, packet.Data.Mouse.dwFlags);
        Assert.Equal(0, packet.Data.Mouse.dx);
        Assert.Equal(0, packet.Data.Mouse.dy);
    }

    [Fact]
    public void Relative_move_does_not_set_the_absolute_flag()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        primitives.MoveBy(10, -20);

        Assert.Equal(NativeInputConstants.MouseEventMove, native.Packets[0].Data.Mouse.dwFlags);
        Assert.Equal(10, native.Packets[0].Data.Mouse.dx);
        Assert.Equal(-20, native.Packets[0].Data.Mouse.dy);
    }

    [Fact]
    public void Absolute_move_outside_the_screen_sends_nothing()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult result = primitives.MoveTo(Primary, 1920, 0);

        Assert.Equal(PrimitiveDelivery.OutsideScreen, result.Delivery);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void Wheel_notches_scale_by_the_Windows_delta()
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        primitives.Wheel(-1);

        Assert.Equal(NativeInputConstants.MouseEventWheel, native.Packets[0].Data.Mouse.dwFlags);
        Assert.Equal(unchecked((uint)-120), native.Packets[0].Data.Mouse.mouseData);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-101)]
    public void Wheel_notches_outside_the_schema_range_are_rejected(int notches)
    {
        var native = new CapturingNative();
        var primitives = new InputPrimitives(new SendInputBatch(native));

        Assert.Throws<ArgumentOutOfRangeException>(() => primitives.Wheel(notches));
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void A_short_click_batch_is_reported_and_not_retried()
    {
        var native = new CapturingNative { Accepted = 1, Win32Error = 5 };
        var primitives = new InputPrimitives(new SendInputBatch(native));

        PrimitiveResult result = primitives.Click(MouseButton.Left);

        Assert.Equal(PrimitiveDelivery.Sent, result.Delivery);
        Assert.False(result.Submission.IsFull);
        Assert.Equal(1, result.Submission.Accepted);
        Assert.Equal(2, result.Submission.Requested);
        Assert.Equal(5, result.Submission.Win32Error);
        Assert.Equal(1, native.Calls);
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<NativeInput> Packets { get; } = [];

        internal uint? Accepted { get; init; }

        internal int Win32Error { get; init; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            Packets.AddRange(inputs);
            uint accepted = Accepted ?? (uint)inputs.Length;
            return (accepted, Win32Error);
        }
    }
}
