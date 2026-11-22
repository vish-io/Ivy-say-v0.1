using IvySay;
using Xunit;

public class ShortcutBindingTests
{
    [Theory]
    [InlineData("CtrlWinSpace", 10u)]
    [InlineData("CtrlShiftSpace", 6u)]
    [InlineData("AltShiftSpace", 5u)]
    public void MigratesLegacyOptions(string legacy, uint modifiers)
    {
        var binding = ShortcutBinding.From(new Preferences { Shortcut = legacy });
        Assert.Equal(32, binding.Key); Assert.Equal(modifiers, binding.Modifiers); Assert.False(binding.Hold);
    }
    [Fact] public void CustomBindingPersistsItsModeAndModifiers()
    {
        var binding = ShortcutBinding.From(new Preferences { ShortcutKey = 0x78, ShortcutModifiers = 3, ShortcutMode = "Hold" });
        Assert.True(binding.Valid); Assert.True(binding.Hold); Assert.Equal(3u, binding.Modifiers);
    }
    [Theory]
    [InlineData(0)] [InlineData(0x10)] [InlineData(0x11)] [InlineData(0x5B)] [InlineData(255)]
    public void RejectsInvalidAndModifierOnlyKeys(int key) => Assert.False(new ShortcutBinding(key, 0, true).Valid);
}
