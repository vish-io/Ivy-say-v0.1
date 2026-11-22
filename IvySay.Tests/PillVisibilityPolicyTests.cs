using IvySay;
using Xunit;

public class PillVisibilityPolicyTests
{
    [Fact] public void OnlyHidesWhenEnabledAndReachableAndIdle()
    {
        Assert.True(PillVisibilityPolicy.ShouldHide(true, true, false, false, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(false, true, false, false, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, false, false, false, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, true, false, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, false, true, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, false, false, true));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, false, false, false, false));
    }
    [Fact] public void HidePreferenceDefaultsOff() => Assert.False(new Preferences().HidePillWhenIdle);
}
