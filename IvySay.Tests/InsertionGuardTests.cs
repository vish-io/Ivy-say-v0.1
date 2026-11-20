using IvySay;
using Xunit;

public class InsertionGuardTests
{
    [Fact] public void NormalTextPasses() => Assert.Null(InsertionGuard.ReviewReason("Hello, world!", false, false, "Guarded"));
    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("a\tb")]
    [InlineData("secret\u001b[31m")]
    [InlineData("hidden\u202Etext")]
    public void RiskyCharactersRequireReview(string text) => Assert.NotNull(InsertionGuard.ReviewReason(text, false, false, "Guarded"));
    [Fact] public void UnknownOrConsoleTargetsRequireReview()
    {
        Assert.NotNull(InsertionGuard.ReviewReason("hello", true, false, "Guarded"));
        Assert.NotNull(InsertionGuard.ReviewReason("hello", false, true, "Guarded"));
        Assert.NotNull(InsertionGuard.ReviewReason("hello", false, false, "ReviewAll"));
    }
    [Fact] public void UnrestrictedBypassesOptionalReviewChecks()
    {
        Assert.Null(InsertionGuard.ReviewReason("a\n\tb\u001b", true, true, "Unrestricted"));
    }
    [Fact] public void InvalidLevelFailsClosedToGuardedChecks()
    {
        Assert.NotNull(InsertionGuard.ReviewReason("line\nline", false, false, "invalid"));
    }
    [Fact] public void LegacySettingsMigrateSafely()
    {
        Assert.Equal("ReviewAll", Preferences.NormalizeSecurityLevel(null, true));
        Assert.Equal("Guarded", Preferences.NormalizeSecurityLevel(null, false));
        Assert.Equal("Unrestricted", Preferences.NormalizeSecurityLevel("Unrestricted", true));
        Assert.Equal("Guarded", Preferences.NormalizeSecurityLevel("invalid", true));
    }
    [Fact] public void UnsafeCharactersBlockedFromCopyButNormalMultilineAllowed()
    {
        Assert.True(InsertionGuard.HasUnsafeCharacters("\u001b[2J"));
        Assert.True(InsertionGuard.HasUnsafeCharacters("\u202E"));
        Assert.True(InsertionGuard.HasUnsafeCharacters("\uD800"));
        Assert.False(InsertionGuard.HasUnsafeCharacters("hello\nworld 😀"));
    }
}
