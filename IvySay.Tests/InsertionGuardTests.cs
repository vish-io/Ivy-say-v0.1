using IvySay;
using Xunit;

public class InsertionGuardTests
{
    [Fact] public void NormalTextPasses() => Assert.Null(InsertionGuard.ReviewReason("Hello, world!", false, false, false));
    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("a\tb")]
    [InlineData("secret\u001b[31m")]
    [InlineData("hidden\u202Etext")]
    public void RiskyCharactersRequireReview(string text) => Assert.NotNull(InsertionGuard.ReviewReason(text, false, false, false));
    [Fact] public void UnknownOrConsoleTargetsRequireReview()
    {
        Assert.NotNull(InsertionGuard.ReviewReason("hello", true, false, false));
        Assert.NotNull(InsertionGuard.ReviewReason("hello", false, true, false));
        Assert.NotNull(InsertionGuard.ReviewReason("hello", false, false, true));
    }
    [Fact] public void UnsafeCharactersBlockedFromCopyButNormalMultilineAllowed()
    {
        Assert.True(InsertionGuard.HasUnsafeCharacters("\u001b[2J"));
        Assert.True(InsertionGuard.HasUnsafeCharacters("\u202E"));
        Assert.True(InsertionGuard.HasUnsafeCharacters("\uD800"));
        Assert.False(InsertionGuard.HasUnsafeCharacters("hello\nworld 😀"));
    }
}
