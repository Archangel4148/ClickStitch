namespace ClickStitch.Input.Tests;

public sealed class ToggleChordTests
{
    [Fact]
    public void A_function_key_is_a_shortcut_and_a_bare_letter_is_not()
    {
        Assert.True(ToggleChord.TryParse("F6", out ToggleBinding binding, out string problem));
        Assert.Equal("", problem);
        Assert.Equal("F6", binding.Text);
        Assert.Equal(0u, binding.Modifiers);
        Assert.Equal(0x75, binding.VirtualKey);

        Assert.False(ToggleChord.TryParse("A", out _, out problem));
        Assert.Contains("function key", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Modifiers_are_kept_in_a_stable_order()
    {
        Assert.True(ToggleChord.TryParse("Shift+Ctrl+F8", out ToggleBinding binding, out _));
        Assert.Equal("Ctrl+Shift+F8", binding.Text);
        Assert.Equal(0x0002u | 0x0004u, binding.Modifiers);
        Assert.Equal(0x77, binding.VirtualKey);
        Assert.False(ToggleChord.TryParse("Ctrl+Shift", out _, out _));
        Assert.False(ToggleChord.TryParse("Ctrl+A+F6", out _, out _));
    }
}
