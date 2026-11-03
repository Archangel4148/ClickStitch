namespace ClickStitch.Input;

/// <summary>
/// The single record of buttons and keys this process has successfully pressed and not yet released.
/// A second down of an owned input is refused, so two callers cannot both believe they hold it.
/// The lock covers the check and the <c>SendInput</c> call. It is not held across a wait.
/// Startup does not guess about keys that were left down by a previous process.
/// </summary>
internal sealed class InputStateManager
{
    private readonly SendInputBatch _batch;
    private readonly Lock _gate = new();
    private readonly HashSet<OwnedInput> _owned = [];
    private object? _boundWorker;

    internal InputStateManager(SendInputBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _batch = batch;
    }

    internal static InputStateManager CreateForWindows() => new(SendInputBatch.CreateForWindows());

    /// <summary>
    /// One execution worker may own this state. A second worker is refused so two threads cannot submit input for it.
    /// </summary>
    internal void BindWorker(object worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        if (Interlocked.CompareExchange(ref _boundWorker, worker, null) != null)
        {
            throw new InvalidOperationException("This input state already has an execution worker.");
        }
    }

    internal void UnbindWorker(object worker) => Interlocked.CompareExchange(ref _boundWorker, null, worker);

    internal OwnedInput[] Snapshot()
    {
        lock (_gate)
        {
            return CopyOwned();
        }
    }

    internal OwnershipResult MoveTo(VirtualScreen screen, int x, int y) => Execute(GesturePlanner.MoveTo(screen, x, y));

    internal OwnershipResult MoveBy(int dx, int dy) => Execute(GesturePlanner.MoveBy(dx, dy));

    internal OwnershipResult MouseDown(MouseButton button) => Execute(GesturePlanner.MouseDown(button));

    internal OwnershipResult MouseUp(MouseButton button) => Execute(GesturePlanner.MouseUp(button));

    internal OwnershipResult MouseDownAt(VirtualScreen screen, int x, int y, MouseButton button) =>
        Execute(GesturePlanner.MouseDownAt(screen, x, y, button));

    internal OwnershipResult MouseUpAt(VirtualScreen screen, int x, int y, MouseButton button) =>
        Execute(GesturePlanner.MouseUpAt(screen, x, y, button));

    internal OwnershipResult MouseDownBy(int dx, int dy, MouseButton button) =>
        Execute(GesturePlanner.MouseDownBy(dx, dy, button));

    internal OwnershipResult MouseUpBy(int dx, int dy, MouseButton button) =>
        Execute(GesturePlanner.MouseUpBy(dx, dy, button));

    internal OwnershipResult Click(MouseButton button, int count = 1) => Execute(GesturePlanner.Click(button, count));

    internal OwnershipResult ClickAt(VirtualScreen screen, int x, int y, MouseButton button, int count = 1) =>
        Execute(GesturePlanner.ClickAt(screen, x, y, button, count));

    internal OwnershipResult ClickBy(int dx, int dy, MouseButton button, int count = 1) =>
        Execute(GesturePlanner.ClickBy(dx, dy, button, count));

    internal OwnershipResult Wheel(int notches) => Execute(GesturePlanner.Wheel(notches));

    internal OwnershipResult KeyDown(InputKey key) => Execute(GesturePlanner.KeyDown(key));

    internal OwnershipResult KeyUp(InputKey key) => Execute(GesturePlanner.KeyUp(key));

    /// <summary>
    /// Submits an up for every owned input, in mouse-then-key order.
    /// An input leaves the set only after Windows accepts its up. A second call with an empty set submits nothing.
    /// </summary>
    internal OwnershipResult ReleaseAllOwnedInputs()
    {
        lock (_gate)
        {
            if (_owned.Count == 0)
            {
                return OwnershipResult.NothingHeld(CopyOwned());
            }

            OwnedInput[] held = CopyOwned();
            var events = new PlannedEvent[held.Length];
            for (int index = 0; index < held.Length; index++)
            {
                events[index] = ReleaseEvent(held[index]);
            }

            return ExecuteUnlocked(GesturePreparation.Ready(events));
        }
    }

    private OwnershipResult Execute(GesturePreparation prepared)
    {
        lock (_gate)
        {
            return ExecuteUnlocked(prepared);
        }
    }

    private OwnershipResult ExecuteUnlocked(GesturePreparation prepared)
    {
        if (prepared.OutsideScreen)
        {
            return OwnershipResult.Outside(CopyOwned());
        }

        GesturePlan plan = prepared.Plan;
        var simulated = new HashSet<OwnedInput>(_owned);
        foreach (PlannedEvent step in plan.Events)
        {
            if (step.Change == OwnershipChange.Press && !simulated.Add(step.Input))
            {
                return OwnershipResult.AlreadyOwned(step.Input, CopyOwned());
            }

            if (step.Change == OwnershipChange.Release && !simulated.Remove(step.Input))
            {
                return OwnershipResult.NotOwned(step.Input, CopyOwned());
            }
        }

        if (plan.Events.Length == 0)
        {
            return OwnershipResult.NothingHeld(CopyOwned());
        }

        InputSubmission submission = _batch.Submit(plan.Packets());
        ApplyAcceptedPrefix(plan, submission.Accepted);
        return OwnershipResult.Sent(submission, CopyOwned());
    }

    private void ApplyAcceptedPrefix(GesturePlan plan, int accepted)
    {
        int count = Math.Min(accepted, plan.Events.Length);
        for (int index = 0; index < count; index++)
        {
            PlannedEvent step = plan.Events[index];
            if (step.Change == OwnershipChange.Press && !_owned.Add(step.Input))
            {
                throw new InvalidOperationException($"Ownership already contained {step.Input} after Windows accepted the press.");
            }

            if (step.Change == OwnershipChange.Release && !_owned.Remove(step.Input))
            {
                throw new InvalidOperationException($"Ownership did not contain {step.Input} after Windows accepted the release.");
            }
        }
    }

    private OwnedInput[] CopyOwned()
    {
        var items = new OwnedInput[_owned.Count];
        _owned.CopyTo(items);
        Array.Sort(items);
        return items;
    }

    private static PlannedEvent ReleaseEvent(OwnedInput input)
    {
        GesturePreparation prepared = input.Device == OwnedDevice.Mouse
            ? GesturePlanner.MouseUp(input.MouseButton)
            : GesturePlanner.KeyUp(input.KeyboardKey);
        return prepared.Plan.Events[0];
    }
}
