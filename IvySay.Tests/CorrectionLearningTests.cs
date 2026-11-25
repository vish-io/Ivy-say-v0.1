using IvySay;
using Xunit;

public class CorrectionLearningTests
{
    [Theory]
    [InlineData("Send to Prabjot. ", "Send to Prabhjot. ", "Prabhjot")]
    [InlineData("Ask Rajesh. ", "Ask Rajesh. ", null)]
    [InlineData("Send Tuesday. ", "Send Thursday. ", null)]
    [InlineData("send me this", "send you that", null)]
    [InlineData("code 1234", "code 1235", null)]
    [InlineData("Ask rajesh.", "Ask Rajesh.", null)]
    public void LearnsOnlySingleSmallWordEdits(string inserted, string edited, string? expected) => Assert.Equal(expected, CorrectionLearning.Candidate(inserted, edited));
    [Fact] public void FindsOnlyUniqueBoundedRegion()
    {
        Assert.Equal("Prabhjot", CorrectionLearning.Extract("long prefix abc Prabhjot long suffix xyz", "long prefix abc ", " long suffix xyz", false, false, false, false));
        Assert.Null(CorrectionLearning.Extract("long prefix abc X long prefix abc Y long suffix xyz", "long prefix abc ", " long suffix xyz", false, false, false, false));
        Assert.Equal("word", CorrectionLearning.Extract("word", "", "", true, true, true, true));
        Assert.Null(CorrectionLearning.Extract("word", "", "", true, true, false, true));
        Assert.Null(CorrectionLearning.Extract("prefix word suffix", "prefix ", " suffix", false, false, true, true));
    }
    [Fact] public void AddsWithoutDuplicatesOrReplacementRules()
    {
        Assert.Equal("Prabhjot", CorrectionLearning.Add("", "Prabhjot"));
        Assert.Equal("Prabhjot", CorrectionLearning.Add("Prabhjot", "prabhjot"));
        Assert.Equal("Prabhjot", CorrectionLearning.Add("Prabhjot", "abc123"));
        Assert.False(new Preferences().LearnCorrections);
        Assert.False(Preferences.ParseExisting("{}").LearnCorrections);
    }
}
