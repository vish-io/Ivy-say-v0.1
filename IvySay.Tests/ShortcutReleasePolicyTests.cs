using IvySay;
using Xunit;

public class ShortcutReleasePolicyTests
{
    [Fact] public void ConsumedHoldTriggerUsesHookStateNotStaleOsState()
    {
        Assert.True(ShortcutReleasePolicy.IsHeld(true, true, false, false));
        Assert.False(ShortcutReleasePolicy.IsHeld(true, false, true, false));
        Assert.True(ShortcutReleasePolicy.IsHeld(true, false, true, true));
        Assert.True(ShortcutReleasePolicy.IsHeld(true, null, true, false));
    }
    [Fact] public void ToggleStillChecksOsAndModifiers()
    {
        Assert.True(ShortcutReleasePolicy.IsHeld(false, false, true, false));
        Assert.True(ShortcutReleasePolicy.IsHeld(false, false, false, true));
        Assert.False(ShortcutReleasePolicy.IsHeld(false, true, false, false));
    }
    [Fact] public void LearningFeedbackReportsProgressWithoutText()
    {
        LearningFeedback.Start(); Assert.Contains("25 seconds", LearningFeedback.Message);
        LearningFeedback.Cancel("Stopped before check"); Assert.Equal("Stopped before check", LearningFeedback.Message);
        LearningFeedback.Finish("A word was saved."); LearningFeedback.Cancel("Should not replace completion");
        Assert.Equal("A word was saved.", LearningFeedback.Message);
    }
}
