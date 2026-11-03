namespace ClickStitch.Input;

/// <summary>
/// How <see cref="DeadlineWait"/> parks the thread until a deadline.
/// The product choice is <see cref="DeadlineWait.ProductStrategy"/>, set from the milestone 1.4 measurements.
/// </summary>
internal enum WaitStrategy
{
    /// <summary>
    /// The simple scheduler: <c>WaitForSingleObject</c> on a cancellation event, with a millisecond timeout.
    /// Resolution follows the system timer. This is the baseline the measurements compare against.
    /// </summary>
    CoarseSleep = 0,

    /// <summary>
    /// A one-shot waitable timer created with <c>CREATE_WAITABLE_TIMER_HIGH_RESOLUTION</c>.
    /// </summary>
    HighResolutionTimer = 1,

    /// <summary>
    /// High-resolution timer until the spin window, then a tight timestamp loop that does not yield.
    /// The spin exists so a short click interval is not rounded up to the timer resolution.
    /// </summary>
    Hybrid = 2,
}
