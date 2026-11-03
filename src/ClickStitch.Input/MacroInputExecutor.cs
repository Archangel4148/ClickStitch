using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// Performs one click, move, hold, wheel, key, or wait through <see cref="InputStateManager"/>.
/// It does not start the execution worker and does not release inputs when an action fails.
/// A repeat is never passed here. Pass the same <see cref="MacroPause"/> to <see cref="MacroRunner"/>.
/// </summary>
internal readonly record struct MacroWaitSlice(int ElapsedMilliseconds, bool Cancelled);

internal sealed class MacroInputExecutor : IMacroExecutor
{
    private readonly InputStateManager _inputs;
    private readonly Func<int, CancellationToken, MacroWaitSlice> _wait;
    private readonly Func<VirtualScreen> _screen;
    private readonly MacroPause _pause;
    private readonly CancellationToken _cancellation;
    private readonly Action<MacroProgressEvent>? _progress;

    internal MacroInputExecutor(
        InputStateManager inputs,
        Func<int, CancellationToken, MacroWaitSlice> wait,
        Func<VirtualScreen> screen,
        MacroPause pause,
        CancellationToken cancellation = default,
        Action<MacroProgressEvent>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(pause);
        _inputs = inputs;
        _wait = wait;
        _screen = screen;
        _pause = pause;
        _cancellation = cancellation;
        _progress = progress;
    }

    /// <summary>
    /// A zero-length wait reports no elapsed time. Any other duration uses <paramref name="wait"/>.
    /// <see cref="MacroWaitSlice.Cancelled"/> is true when the wait returned before the deadline.
    /// </summary>
    internal static Func<int, CancellationToken, MacroWaitSlice> WaitWith(DeadlineWait wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        return (milliseconds, cancellation) =>
        {
            if (milliseconds == 0)
            {
                return new MacroWaitSlice(0, cancellation.IsCancellationRequested);
            }

            long started = MonotonicClock.Timestamp;
            ScheduleWake wake = wait.WaitUntil(started + MonotonicClock.TicksFromMilliseconds(milliseconds), cancellation);
            int elapsed = wake.Timestamp <= started
                ? 0
                : (int)Math.Min(int.MaxValue, MonotonicClock.Milliseconds(wake.Timestamp - started));
            return new MacroWaitSlice(elapsed, wake.Cancelled);
        };
    }

    public void Click(ClickContract action)
    {
        MouseButton button = ToButton(action.Button);
        OwnershipResult result = action.Point is not MacroPoint point
            ? _inputs.Click(button, action.ClickCount)
            : point.Mode switch
            {
                MacroCoordinateMode.VirtualDesktop or MacroCoordinateMode.PrimaryDisplay =>
                    _inputs.ClickAt(_screen(), point.X, point.Y, button, action.ClickCount),
                MacroCoordinateMode.Relative => _inputs.ClickBy(point.X, point.Y, button, action.ClickCount),
                _ => throw new InvalidOperationException($"Click {action.ActionId} cannot use {point.Mode}."),
            };
        Require(result);
    }

    public void Move(MoveContract action)
    {
        MacroPoint point = action.Point;
        OwnershipResult result = point.Mode switch
        {
            MacroCoordinateMode.VirtualDesktop or MacroCoordinateMode.PrimaryDisplay => _inputs.MoveTo(_screen(), point.X, point.Y),
            MacroCoordinateMode.Relative => _inputs.MoveBy(point.X, point.Y),
            _ => throw new InvalidOperationException($"Move {action.ActionId} cannot use {point.Mode}."),
        };
        Require(result);
    }

    public void Hold(HoldContract action)
    {
        MouseButton button = ToButton(action.Button);
        bool down = action.State == MacroPressState.Down;
        OwnershipResult result = action.Point is not MacroPoint point
            ? down ? _inputs.MouseDown(button) : _inputs.MouseUp(button)
            : point.Mode switch
            {
                MacroCoordinateMode.VirtualDesktop or MacroCoordinateMode.PrimaryDisplay => down
                    ? _inputs.MouseDownAt(_screen(), point.X, point.Y, button)
                    : _inputs.MouseUpAt(_screen(), point.X, point.Y, button),
                MacroCoordinateMode.Relative => down
                    ? _inputs.MouseDownBy(point.X, point.Y, button)
                    : _inputs.MouseUpBy(point.X, point.Y, button),
                _ => throw new InvalidOperationException($"Hold {action.ActionId} cannot use {point.Mode}."),
            };
        Require(result);
    }

    public void Wheel(WheelContract action) => Require(_inputs.Wheel(action.Notches));

    public void Key(KeyContract action)
    {
        if (!Enum.TryParse(action.Key, out InputKey key) || !Enum.IsDefined(key))
        {
            throw new InvalidOperationException($"Key {action.Key} is not in the macro schema.");
        }

        OwnershipResult result = action.State == MacroPressState.Down ? _inputs.KeyDown(key) : _inputs.KeyUp(key);
        Require(result);
    }

    public void Wait(WaitContract action)
    {
        int remaining = action.DurationMs;
        while (true)
        {
            bool paused = _pause.IsPaused;
            if (paused)
            {
                _progress?.Invoke(new MacroProgressEvent(MacroProgressKind.Paused, action.ActionId, null));
            }

            if (!_pause.WaitUntilRunning(_cancellation))
            {
                throw new OperationCanceledException(_cancellation);
            }

            if (paused)
            {
                _progress?.Invoke(new MacroProgressEvent(MacroProgressKind.Resumed, action.ActionId, null));
            }

            using var slice = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation, slice.Token);
            _pause.AttachWait(slice);
            MacroWaitSlice slept;
            try
            {
                slept = _wait(remaining, linked.Token);
            }
            finally
            {
                _pause.DetachWait(slice);
            }

            if (slept.ElapsedMilliseconds < 0)
            {
                throw new InvalidOperationException("A wait reported negative elapsed time.");
            }

            if (_cancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException(_cancellation);
            }

            if (!slept.Cancelled)
            {
                return;
            }

            remaining = slept.ElapsedMilliseconds >= remaining ? 0 : remaining - slept.ElapsedMilliseconds;
            if (remaining == 0 || !_pause.IsPaused)
            {
                throw new InvalidOperationException("The wait ended early.");
            }
        }
    }

    /// <summary>
    /// The primary display origin is virtual-desktop (0, 0), so that mode uses the same absolute move.
    /// This method does not clip the point to the primary monitor.
    /// </summary>
    private static void Require(OwnershipResult result)
    {
        if (result.Outcome == OwnershipOutcome.Sent && result.Submission.IsFull)
        {
            return;
        }

        string message = result.Outcome switch
        {
            OwnershipOutcome.Sent =>
                $"SendInput accepted {result.Submission.Accepted} of {result.Submission.Requested}.",
            OwnershipOutcome.OutsideScreen => "The point is outside the virtual screen.",
            OwnershipOutcome.AlreadyOwned => "The input is already held.",
            OwnershipOutcome.NotOwned => "The input is not held.",
            _ => "The action was not submitted.",
        };
        throw new InvalidOperationException(message);
    }

    private static MouseButton ToButton(MacroMouseButton button) => button switch
    {
        MacroMouseButton.Left => MouseButton.Left,
        MacroMouseButton.Right => MouseButton.Right,
        MacroMouseButton.Middle => MouseButton.Middle,
        _ => throw new InvalidOperationException($"Mouse button {button} is not in the macro schema."),
    };
}
