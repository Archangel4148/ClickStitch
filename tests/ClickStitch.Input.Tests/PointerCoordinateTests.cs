namespace ClickStitch.Input.Tests;

public sealed class PointerCoordinateTests
{
    [Fact]
    public void Primary_screen_corners_map_to_the_normalized_ends()
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, 0, 0, out int left, out int top));
        Assert.Equal(0, left);
        Assert.Equal(0, top);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, 1919, 1079, out int right, out int bottom));
        Assert.Equal(65535, right);
        Assert.Equal(65535, bottom);
    }

    [Fact]
    public void Interior_pixel_truncates_toward_zero()
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, 959, 0, out int absoluteX, out int absoluteY));

        Assert.Equal(32750, absoluteX);
        Assert.Equal(0, absoluteY);
    }

    [Fact]
    public void Negative_virtual_origin_is_the_zero_point()
    {
        var screen = new VirtualScreen(-1920, 0, 3840, 1080);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, -1920, 0, out int originX, out int originY));
        Assert.Equal(0, originX);
        Assert.Equal(0, originY);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, 0, 0, out int seamX, out _));
        Assert.Equal(32776, seamX);

        Assert.True(PointerCoordinates.TryToAbsolute(screen, 1919, 1079, out int farX, out int farY));
        Assert.Equal(65535, farX);
        Assert.Equal(65535, farY);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1920, 0)]
    [InlineData(0, 1080)]
    public void Pixels_outside_the_screen_are_rejected(int x, int y)
    {
        var screen = new VirtualScreen(0, 0, 1920, 1080);

        bool inside = PointerCoordinates.TryToAbsolute(screen, x, y, out int absoluteX, out int absoluteY);

        Assert.False(inside);
        Assert.Equal(0, absoluteX);
        Assert.Equal(0, absoluteY);
    }

    [Fact]
    public void A_one_pixel_screen_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualScreen(0, 0, 1, 1080));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualScreen(0, 0, 1920, 1));
    }

    [Fact]
    public void Relative_deltas_pass_through_the_schema_range()
    {
        (int dx, int dy) = PointerCoordinates.Relative(10, -20);

        Assert.Equal(10, dx);
        Assert.Equal(-20, dy);
        Assert.Equal((short.MaxValue, short.MinValue), PointerCoordinates.Relative(short.MaxValue, short.MinValue));
    }

    [Fact]
    public void Relative_deltas_outside_the_schema_range_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PointerCoordinates.Relative(short.MaxValue + 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PointerCoordinates.Relative(0, short.MinValue - 1));
    }
}
