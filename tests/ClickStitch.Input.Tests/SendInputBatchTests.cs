using System.Runtime.InteropServices;

namespace ClickStitch.Input.Tests;

public sealed class SendInputBatchTests
{
    [Fact]
    public void Empty_batch_does_not_call_native_code()
    {
        var native = new RecordingNative();
        var batch = new SendInputBatch(native);

        InputSubmission submission = batch.Submit([]);

        Assert.True(submission.IsFull);
        Assert.Equal(0, submission.Requested);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void Null_batch_is_rejected()
    {
        var batch = new SendInputBatch(new RecordingNative());

        Assert.Throws<ArgumentNullException>(() => batch.Submit(null!));
    }

    [Fact]
    public void Full_acceptance_drops_a_stale_Win32_error()
    {
        var native = new RecordingNative { Accepted = 1, Win32Error = 5 };
        var batch = new SendInputBatch(native);

        InputSubmission submission = batch.Submit([MouseMove()]);

        Assert.True(submission.IsFull);
        Assert.Equal(1, submission.Accepted);
        Assert.Null(submission.Win32Error);
        Assert.Equal(Marshal.SizeOf<NativeInput>(), native.StructureSize);
    }

    [Fact]
    public void Partial_acceptance_keeps_the_Win32_error()
    {
        var native = new RecordingNative { Accepted = 1, Win32Error = 5 };
        var batch = new SendInputBatch(native);

        InputSubmission submission = batch.Submit([MouseMove(), MouseMove()]);

        Assert.False(submission.IsFull);
        Assert.Equal(2, submission.Requested);
        Assert.Equal(1, submission.Accepted);
        Assert.Equal(5, submission.Win32Error);
    }

    [Fact]
    public void Zero_acceptance_keeps_a_zero_error_code()
    {
        var native = new RecordingNative { Accepted = 0, Win32Error = 0 };
        var batch = new SendInputBatch(native);

        InputSubmission submission = batch.Submit([KeyDown()]);

        Assert.False(submission.IsFull);
        Assert.Equal(0, submission.Accepted);
        Assert.Equal(0, submission.Win32Error);
    }

    [Fact]
    public void Hardware_packets_are_rejected_before_the_native_call()
    {
        var native = new RecordingNative();
        var batch = new SendInputBatch(native);
        var hardware = new NativeInput { type = NativeInputConstants.InputHardware };

        Assert.Throws<ArgumentException>(() => batch.Submit([hardware]));
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public void An_accepted_count_above_the_batch_size_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => InputSubmission.Interpret(1, 2, 0));
    }

    private static NativeInput MouseMove()
    {
        return new NativeInput
        {
            type = NativeInputConstants.InputMouse,
            Data = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    dx = 1,
                    dy = 2,
                    dwFlags = NativeInputConstants.MouseEventMove,
                },
            },
        };
    }

    private static NativeInput KeyDown()
    {
        return new NativeInput
        {
            type = NativeInputConstants.InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    wVk = 0x41,
                    dwFlags = 0,
                },
            },
        };
    }

    private sealed class RecordingNative : INativeSendInput
    {
        internal int Calls { get; private set; }

        internal int StructureSize { get; private set; }

        internal uint Accepted { get; init; }

        internal int Win32Error { get; init; }

        public (uint Accepted, int Win32Error) Send(NativeInput[] inputs, int structureSize)
        {
            Calls++;
            StructureSize = structureSize;
            return (Accepted, Win32Error);
        }
    }
}
