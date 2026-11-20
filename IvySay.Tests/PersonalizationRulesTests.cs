using IvySay;
using Xunit;

public class PersonalizationRulesTests
{
    [Fact] public void AppliesOnlyWholePhrasesIgnoringCase()
    {
        var rules = PersonalizationRules.Parse("grindel wald => Grindelwald\npen => pencil");
        Assert.Equal("Grindelwald and pencil, not open", rules.Apply("GRINDEL WALD and pen, not open"));
    }
    [Fact] public void DoesNotCascadeOrMatchWithinUnicodeWords()
    {
        var rules = PersonalizationRules.Parse("cat => dog\ndog => fox");
        Assert.Equal("dog fox café_cat", rules.Apply("cat dog café_cat"));
    }
    [Fact] public void PrefersLongestOverlappingPhrase()
    {
        var rules = PersonalizationRules.Parse("new => old\nnew york => New York City");
        Assert.Equal("New York City", rules.Apply("new york"));
    }
    [Fact] public void RejectsInvalidAndDuplicateRules()
    {
        Assert.Throws<ArgumentException>(() => PersonalizationRules.Parse("bad line"));
        Assert.Throws<ArgumentException>(() => PersonalizationRules.Parse("one => two\nONE => three"));
    }
    [Fact] public void PromptPrioritizesWholeTermsWithinLimit()
    {
        string output = PersonalizationRules.BuildPrompt("Grindelwald\n" + new string('x', 160), "Ivy Say, Punjab");
        Assert.Equal("Grindelwald, Ivy Say, Punjab", output);
        Assert.True(output.Length <= 160);
    }
}
