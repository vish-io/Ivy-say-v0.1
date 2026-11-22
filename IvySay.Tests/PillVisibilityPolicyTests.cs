using IvySay;
using Xunit;

public class PillVisibilityPolicyTests
{
    [Fact] public void OnlyHidesWhenEnabledAndReachableAndIdle()
    {
        Assert.True(PillVisibilityPolicy.ShouldHide(true, true, false, false, false));
        Assert.False(PillVisibilityPolicy.ShouldHide(false, true, false, false, false));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, false, false, false, false));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, true, false, false));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, false, true, false));
        Assert.False(PillVisibilityPolicy.ShouldHide(true, true, false, false, true));
    }
    [Fact] public void HidePreferenceDefaultsOff() => Assert.False(new Preferences().HidePillWhenIdle);
}
