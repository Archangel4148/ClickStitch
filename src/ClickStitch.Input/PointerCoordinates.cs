namespace ClickStitch.Input;

/// <summary>
/// A virtual desktop in physical pixels. The origin can be negative when a monitor sits left of or above the primary display.
/// </summary>
internal readonly struct VirtualScreen
{
    internal VirtualScreen(int left, int top, int width, int height)
    {
        if (width < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 2 physical pixels.");
        }

        if (height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 2 physical pixels.");
        }

        long right = (long)left + width - 1;
        long bottom = (long)top + height - 1;
        if (right > int.MaxValue || right < int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The virtual screen does not fit in 32-bit coordinates.");
        }

        if (bottom > int.MaxValue || bottom < int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The virtual screen does not fit in 32-bit coordinates.");
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    internal int Left { get; }

    internal int Top { get; }

    internal int Width { get; }

    internal int Height { get; }
}

/// <summary>
/// Converts physical pixels into the numbers <c>SendInput</c> expects.
/// WPF device-independent units are a different measurement and are not accepted here.
/// </summary>
internal static class PointerCoordinates
{
    internal const int NormalizedMaximum = 65535;

    /// <summary>
    /// Maps a physical pixel to the 0–65535 absolute range.
    /// The formula is <c>((pixel - origin) * 65535) / (extent - 1)</c>, with 64-bit math that truncates toward zero.
    /// Pixels outside the screen return false. The out values are then 0 and must not be sent.
    /// </summary>
    internal static bool TryToAbsolute(VirtualScreen screen, int x, int y, out int absoluteX, out int absoluteY)
    {
        long relativeX = (long)x - screen.Left;
        long relativeY = (long)y - screen.Top;
        if (relativeX < 0 || relativeY < 0 || relativeX >= screen.Width || relativeY >= screen.Height)
        {
            absoluteX = 0;
            absoluteY = 0;
            return false;
        }

        absoluteX = (int)(relativeX * NormalizedMaximum / (screen.Width - 1L));
        absoluteY = (int)(relativeY * NormalizedMaximum / (screen.Height - 1L));
        return true;
    }

    /// <summary>
    /// Passes through a relative delta in the macro schema range, -32768 through 32767.
    /// A relative <c>SendInput</c> move is pointer motion. Windows may apply acceleration, so the delta is not a pixel promise.
    /// </summary>
    internal static (int Dx, int Dy) Relative(int dx, int dy)
    {
        if (dx is < short.MinValue or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(dx), "Relative x must be from -32768 through 32767.");
        }

        if (dy is < short.MinValue or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(dy), "Relative y must be from -32768 through 32767.");
        }

        return (dx, dy);
    }
}
