namespace ClickStitch.Input;

/// <summary>
/// What Windows did with one <c>SendInput</c> batch.
/// A short acceptance is a result, not an exception. The Win32 error is kept only for a short batch,
/// because a successful call can leave a stale error code behind.
/// </summary>
internal readonly struct InputSubmission
{
    private InputSubmission(int requested, int accepted, int? win32Error)
    {
        Requested = requested;
        Accepted = accepted;
        Win32Error = win32Error;
    }

    internal int Requested { get; }

    internal int Accepted { get; }

    internal int? Win32Error { get; }

    internal bool IsFull => Accepted == Requested;

    internal static InputSubmission Empty { get; } = new(0, 0, null);

    internal static InputSubmission Interpret(int requested, uint accepted, int win32Error)
    {
        if (requested < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requested));
        }

        if (accepted > (uint)requested)
        {
            throw new InvalidOperationException(
                $"SendInput reported {accepted} accepted events for a batch of {requested}.");
        }

        int acceptedCount = (int)accepted;
        if (acceptedCount == requested)
        {
            return new InputSubmission(requested, acceptedCount, null);
        }

        return new InputSubmission(requested, acceptedCount, win32Error);
    }
}
