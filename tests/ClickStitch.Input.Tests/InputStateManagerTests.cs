namespace ClickStitch.Input.Tests;

public sealed class InputStateManagerTests
{
    private static readonly VirtualScreen Primary = new(0, 0, 1920, 1080);

    [Fact]
    public void A_new_manager_owns_nothing()
    {
        var manager = new InputStateManager(new SendInputBatch(new CapturingNative()));

        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Key_down_is_owned_and_a_second_down_is_refused()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));

        OwnershipResult first = manager.KeyDown(InputKey.Shift);
        OwnershipResult second = manager.KeyDown(InputKey.Shift);

        Assert.Equal(OwnershipOutcome.Sent, first.Outcome);
        Assert.Equal([OwnedInput.Key(InputKey.Shift)], first.StillOwned);
        Assert.Equal(OwnershipOutcome.AlreadyOwned, second.Outcome);
        Assert.Equal(OwnedInput.Key(InputKey.Shift), second.Conflict);
        Assert.Equal(1, native.Calls);
        Assert.Equal([OwnedInput.Key(InputKey.Shift)], manager.Snapshot());
    }

    [Fact]
    public void A_different_key_can_be_held_at_the_same_time()
    {
        var manager = new InputStateManager(new SendInputBatch(new CapturingNative()));

        manager.KeyDown(InputKey.Shift);
        manager.KeyDown(InputKey.A);

        Assert.Equal(
            [OwnedInput.Key(InputKey.A), OwnedInput.Key(InputKey.Shift)],
            manager.Snapshot());
    }

    [Fact]
    public void Key_up_removes_ownership_and_a_second_up_is_not_sent()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.KeyDown(InputKey.A);

        OwnershipResult up = manager.KeyUp(InputKey.A);
        int callsAfterUp = native.Calls;
        OwnershipResult again = manager.KeyUp(InputKey.A);

        Assert.Equal(OwnershipOutcome.Sent, up.Outcome);
        Assert.Empty(up.StillOwned);
        Assert.Equal(OwnershipOutcome.NotOwned, again.Outcome);
        Assert.Equal(OwnedInput.Key(InputKey.A), again.Conflict);
        Assert.Equal(callsAfterUp, native.Calls);
        Assert.Empty(manager.Snapshot());
    }

    [Fact]
    public void Mouse_buttons_are_tracked_independently()
    {
        var manager = new InputStateManager(new SendInputBatch(new CapturingNative()));

        manager.MouseDown(MouseButton.Right);
        OwnershipResult secondLeft = manager.MouseDown(MouseButton.Left);
        OwnershipResult repeatedRight = manager.MouseDown(MouseButton.Right);

        Assert.Equal(OwnershipOutcome.Sent, secondLeft.Outcome);
        Assert.Equal(OwnershipOutcome.AlreadyOwned, repeatedRight.Outcome);
        Assert.Equal(
            [OwnedInput.Mouse(MouseButton.Left), OwnedInput.Mouse(MouseButton.Right)],
            manager.Snapshot());
    }

    [Fact]
    public void A_full_click_does_not_leave_the_button_held()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));

        OwnershipResult result = manager.Click(MouseButton.Left, 2);

        Assert.Equal(OwnershipOutcome.Sent, result.Outcome);
        Assert.True(result.Submission.IsFull);
        Assert.Equal(4, native.Packets.Count);
        Assert.Empty(result.StillOwned);
    }

    [Fact]
    public void A_click_accepted_only_through_the_down_leaves_the_button_held()
    {
        var native = new CapturingNative { Accepted = 1 };
        var manager = new InputStateManager(new SendInputBatch(native));

        OwnershipResult result = manager.Click(MouseButton.Left);

        Assert.Equal(OwnershipOutcome.Sent, result.Outcome);
        Assert.False(result.Submission.IsFull);
        Assert.Equal([OwnedInput.Mouse(MouseButton.Left)], result.StillOwned);
        Assert.Equal(1, native.Calls);
    }

    [Fact]
    public void A_double_click_accepted_through_the_second_down_leaves_the_button_held()
    {
        var native = new CapturingNative { Accepted = 3 };
        var manager = new InputStateManager(new SendInputBatch(native));

        OwnershipResult result = manager.Click(MouseButton.Middle, 2);

        Assert.Equal([OwnedInput.Mouse(MouseButton.Middle)], result.StillOwned);
    }

    [Fact]
    public void A_click_is_refused_while_that_button_is_already_held()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.MouseDown(MouseButton.Left);
        int calls = native.Calls;

        OwnershipResult result = manager.Click(MouseButton.Left);

        Assert.Equal(OwnershipOutcome.AlreadyOwned, result.Outcome);
        Assert.Equal(calls, native.Calls);
        Assert.Equal([OwnedInput.Mouse(MouseButton.Left)], result.StillOwned);
    }

    [Fact]
    public void Release_all_sends_ups_in_mouse_then_key_order_and_clears_ownership()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.KeyDown(InputKey.A);
        manager.MouseDown(MouseButton.Right);
        manager.KeyDown(InputKey.Shift);
        int beforeRelease = native.Packets.Count;

        OwnershipResult released = manager.ReleaseAllOwnedInputs();
        List<NativeInput> releasePackets = native.Packets.GetRange(beforeRelease, native.Packets.Count - beforeRelease);

        Assert.Equal(OwnershipOutcome.Sent, released.Outcome);
        Assert.Empty(released.StillOwned);
        Assert.Equal(3, releasePackets.Count);
        Assert.Equal(NativeInputConstants.MouseEventRightUp, releasePackets[0].Data.Mouse.dwFlags);
        Assert.Equal(0x41, releasePackets[1].Data.Keyboard.wVk);
        Assert.Equal(NativeInputConstants.KeyEventKeyUp, releasePackets[1].Data.Keyboard.dwFlags);
        Assert.Equal(0x10, releasePackets[2].Data.Keyboard.wVk);
    }

    [Fact]
    public void Release_all_is_idempotent()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.KeyDown(InputKey.Enter);
        manager.ReleaseAllOwnedInputs();
        int calls = native.Calls;

        OwnershipResult again = manager.ReleaseAllOwnedInputs();

        Assert.Equal(OwnershipOutcome.NothingHeld, again.Outcome);
        Assert.Equal(calls, native.Calls);
        Assert.Empty(again.StillOwned);
    }

    [Fact]
    public void A_short_release_keeps_the_inputs_Windows_did_not_accept()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.MouseDown(MouseButton.Left);
        manager.KeyDown(InputKey.A);
        native.Accepted = 1;
        native.Win32Error = 5;

        OwnershipResult released = manager.ReleaseAllOwnedInputs();

        Assert.Equal(OwnershipOutcome.Sent, released.Outcome);
        Assert.False(released.Submission.IsFull);
        Assert.Equal(5, released.Submission.Win32Error);
        Assert.Equal([OwnedInput.Key(InputKey.A)], released.StillOwned);
        Assert.Equal([OwnedInput.Key(InputKey.A)], manager.Snapshot());
    }

    [Fact]
    public void Move_and_wheel_do_not_change_ownership()
    {
        var manager = new InputStateManager(new SendInputBatch(new CapturingNative()));
        manager.KeyDown(InputKey.Control);

        OwnershipResult moved = manager.MoveBy(4, 0);
        OwnershipResult wheeled = manager.Wheel(1);

        Assert.Equal(OwnershipOutcome.Sent, moved.Outcome);
        Assert.Equal(OwnershipOutcome.Sent, wheeled.Outcome);
        Assert.Equal([OwnedInput.Key(InputKey.Control)], manager.Snapshot());
    }

    [Fact]
    public void A_point_outside_the_screen_sends_nothing_and_keeps_ownership()
    {
        var native = new CapturingNative();
        var manager = new InputStateManager(new SendInputBatch(native));
        manager.MouseDown(MouseButton.Left);
        int calls = native.Calls;

        OwnershipResult result = manager.ClickAt(Primary, -1, 0, MouseButton.Left);

        Assert.Equal(OwnershipOutcome.OutsideScreen, result.Outcome);
        Assert.Equal(calls, native.Calls);
        Assert.Equal([OwnedInput.Mouse(MouseButton.Left)], result.StillOwned);
    }

    [Fact]
    public void Mouse_down_at_a_point_owns_the_button_only_after_the_button_packet_is_accepted()
    {
        var moveOnly = new CapturingNative { Accepted = 1 };
        var moveOnlyManager = new InputStateManager(new SendInputBatch(moveOnly));
        OwnershipResult partial = moveOnlyManager.MouseDownAt(Primary, 0, 0, MouseButton.Left);

        var both = new CapturingNative();
        var bothManager = new InputStateManager(new SendInputBatch(both));
        OwnershipResult full = bothManager.MouseDownAt(Primary, 0, 0, MouseButton.Left);

        Assert.Empty(partial.StillOwned);
        Assert.Equal([OwnedInput.Mouse(MouseButton.Left)], full.StillOwned);
    }

    private sealed class CapturingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal List<NativeInput> Packets { get; } = [];

        internal uint? Accepted { get; set; }

        internal int Win32Error { get; set; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            Packets.AddRange(inputs);
            uint accepted = Accepted ?? (uint)inputs.Length;
            return (accepted, Win32Error);
        }
    }
}
