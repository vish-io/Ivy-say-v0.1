using IvySay;
using Xunit;

public class SnippetRulesTests
{
    [Fact] public void MatchesOnlyWholeUtterancesIgnoringCaseAndTrailingPunctuation()
    {
        var snippets = SnippetRules.Parse("my signature => Kind regards, Ivy\nhello => hello there");
        Assert.True(snippets.TryMatch(" MY SIGNATURE! ", out var value));
        Assert.Equal("Kind regards, Ivy", value);
        Assert.False(snippets.TryMatch("please my signature", out _));
        Assert.False(new Preferences().UseSnippets);
    }
    [Fact] public void RejectsMalformedAndDuplicateTriggers()
    {
        Assert.Throws<ArgumentException>(() => SnippetRules.Parse("missing separator"));
        Assert.Throws<ArgumentException>(() => SnippetRules.Parse("hello => one\nHello! => two"));
    }
}
