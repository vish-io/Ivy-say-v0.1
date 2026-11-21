using System.Text.Json;
using IvySay;
using Xunit;

public class FormatterSelectionTests
{
    [Theory]
    [InlineData("Hello, how are you?")]
    [InlineData("No thank you, I like it.")]
    [InlineData("I prefer tea instead of coffee.")]
    [InlineData("Actually I enjoyed that movie.")]
    public void OrdinarySpeechUses20B(string text)
    {
        var choice = FormatterSelection.Select(text, true, 120);
        Assert.Equal(FormatterRouter.Primary, choice.Model);
        Assert.Equal("standard dictation", choice.Reason);
    }
    [Theory]
    [InlineData("Use method Y, no, no, no, use method X.")]
    [InlineData("do it the first way no no no do it like this")]
    [InlineData("Tuesday, sorry, make that Thursday")]
    [InlineData("Send it at 3, actually 4 PM")]
    [InlineData("I mean the other document")]
    [InlineData("change that to blue")]
    public void ExplicitCorrectionsRouteTo120B(string text)
    {
        var choice = FormatterSelection.Select(text, true, 120);
        Assert.Equal(FormatterRouter.Fallback, choice.Model);
        Assert.Equal("correction cue", choice.Reason);
    }
    [Fact]
    public void LengthUsesInclusiveThresholdAndCountsUnicodeWords()
    {
        string text = string.Join(" ", Enumerable.Repeat("ਨਮਸਤੇ", 20));
        Assert.Equal(FormatterRouter.Fallback, FormatterSelection.Select(text, true, 20).Model);
        Assert.Equal("long dictation", FormatterSelection.Select(text, true, 20).Reason);
        Assert.Equal(FormatterRouter.Primary, FormatterSelection.Select(text, true, 21).Model);
    }
    [Fact]
    public void DisabledUses20BRegardlessOfLengthOrCorrection()
    {
        var result = FormatterSelection.Select("no no use another method " + string.Join(" ", Enumerable.Repeat("word", 130)), false, 20);
        Assert.Equal(FormatterRouter.Primary, result.Model);
        Assert.Equal("smart routing off", result.Reason);
    }
    [Fact]
    public void InvalidThresholdDefaultsTo120AndOldSettingsEnableRouting()
    {
        var prefs = JsonSerializer.Deserialize<Preferences>("{\"Polish\":true}")!;
        Assert.True(prefs.SmartFormatterRouting);
        Assert.Equal(120, prefs.FormatterWordThreshold);
        string text = string.Join(" ", Enumerable.Repeat("word", 30));
        Assert.Equal(FormatterRouter.Primary, FormatterSelection.Select(text, true, -1).Model);
        Assert.Equal(FormatterRouter.Fallback, FormatterSelection.Select(text, true, 30).Model);
    }
}
