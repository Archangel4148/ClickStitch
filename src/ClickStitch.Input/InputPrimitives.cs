namespace ClickStitch.Input;

/// <summary>
/// Submits one gesture as a batch. It does not remember which buttons or keys are held.
/// Presses that must be tracked go through <see cref="InputStateManager"/>.
/// </summary>
internal sealed class InputPrimitives
{
    private readonly SendInputBatch _batch;

    internal InputPrimitives(SendInputBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _batch = batch;
    }

    internal static InputPrimitives CreateForWindows() => new(SendInputBatch.CreateForWindows());

    internal PrimitiveResult MoveTo(VirtualScreen screen, int x, int y) => Submit(GesturePlanner.MoveTo(screen, x, y));

    internal PrimitiveResult MoveBy(int dx, int dy) => Submit(GesturePlanner.MoveBy(dx, dy));

    internal PrimitiveResult MouseDown(MouseButton button) => Submit(GesturePlanner.MouseDown(button));

    internal PrimitiveResult MouseUp(MouseButton button) => Submit(GesturePlanner.MouseUp(button));

    internal PrimitiveResult MouseDownAt(VirtualScreen screen, int x, int y, MouseButton button) =>
        Submit(GesturePlanner.MouseDownAt(screen, x, y, button));

    internal PrimitiveResult MouseUpAt(VirtualScreen screen, int x, int y, MouseButton button) =>
        Submit(GesturePlanner.MouseUpAt(screen, x, y, button));

    internal PrimitiveResult Click(MouseButton button, int count = 1) => Submit(GesturePlanner.Click(button, count));

    internal PrimitiveResult ClickAt(VirtualScreen screen, int x, int y, MouseButton button, int count = 1) =>
        Submit(GesturePlanner.ClickAt(screen, x, y, button, count));

    internal PrimitiveResult Wheel(int notches) => Submit(GesturePlanner.Wheel(notches));

    internal PrimitiveResult KeyDown(InputKey key) => Submit(GesturePlanner.KeyDown(key));

    internal PrimitiveResult KeyUp(InputKey key) => Submit(GesturePlanner.KeyUp(key));

    private PrimitiveResult Submit(GesturePreparation prepared)
    {
        if (prepared.OutsideScreen)
        {
            return PrimitiveResult.OutsideScreen;
        }

        return PrimitiveResult.Sent(_batch.Submit(prepared.Plan.Packets()));
    }
}
