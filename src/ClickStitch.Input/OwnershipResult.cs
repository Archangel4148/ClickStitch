namespace ClickStitch.Input;

internal enum OwnershipOutcome
{
    /// <summary>A batch was submitted. Check the submission for a short acceptance.</summary>
    Sent,

    /// <summary>The point is outside the virtual screen. Nothing was sent.</summary>
    OutsideScreen,

    /// <summary>An up was requested for an input the engine does not hold. Nothing was sent.</summary>
    NotOwned,

    /// <summary>A down was requested for an input the engine already holds. Nothing was sent.</summary>
    AlreadyOwned,

    /// <summary>Release-all found nothing held. Nothing was sent.</summary>
    NothingHeld,
}

/// <summary>
/// The outcome of one ownership-checked gesture.
/// <see cref="StillOwned"/> is the set after the call. A failed release does not clear it.
/// </summary>
internal readonly struct OwnershipResult
{
    private OwnershipResult(OwnershipOutcome outcome, InputSubmission submission, OwnedInput? conflict, OwnedInput[] stillOwned)
    {
        Outcome = outcome;
        Submission = submission;
        Conflict = conflict;
        StillOwned = stillOwned;
    }

    internal OwnershipOutcome Outcome { get; }

    internal InputSubmission Submission { get; }

    internal OwnedInput? Conflict { get; }

    internal OwnedInput[] StillOwned { get; }

    internal static OwnershipResult Sent(InputSubmission submission, OwnedInput[] stillOwned) =>
        new(OwnershipOutcome.Sent, submission, null, stillOwned);

    internal static OwnershipResult Outside(OwnedInput[] stillOwned) =>
        new(OwnershipOutcome.OutsideScreen, InputSubmission.Empty, null, stillOwned);

    internal static OwnershipResult NotOwned(OwnedInput conflict, OwnedInput[] stillOwned) =>
        new(OwnershipOutcome.NotOwned, InputSubmission.Empty, conflict, stillOwned);

    internal static OwnershipResult AlreadyOwned(OwnedInput conflict, OwnedInput[] stillOwned) =>
        new(OwnershipOutcome.AlreadyOwned, InputSubmission.Empty, conflict, stillOwned);

    internal static OwnershipResult NothingHeld(OwnedInput[] stillOwned) =>
        new(OwnershipOutcome.NothingHeld, InputSubmission.Empty, null, stillOwned);
}
