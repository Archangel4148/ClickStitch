namespace ClickStitch.Input;

/// <summary>
/// Whether a primitive submitted a batch. <see cref="PrimitiveDelivery.OutsideScreen"/> means nothing was sent.
/// A sent batch can still be only partly accepted by Windows. Held-button cleanup is not done here.
/// </summary>
internal readonly struct PrimitiveResult
{
    private PrimitiveResult(PrimitiveDelivery delivery, InputSubmission submission)
    {
        Delivery = delivery;
        Submission = submission;
    }

    internal PrimitiveDelivery Delivery { get; }

    internal InputSubmission Submission { get; }

    internal static PrimitiveResult OutsideScreen { get; } = new(PrimitiveDelivery.OutsideScreen, InputSubmission.Empty);

    internal static PrimitiveResult Sent(InputSubmission submission) => new(PrimitiveDelivery.Sent, submission);
}

internal enum PrimitiveDelivery
{
    Sent,
    OutsideScreen,
}
