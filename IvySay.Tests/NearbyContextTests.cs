using IvySay;
using Xunit;

public class NearbyContextTests
{
    [Fact] public void BothPermissionsDefaultOff()
    {
        Assert.False(new Preferences().UseNearbyText); Assert.False(new Preferences().ShareNearbyText);
        var old = Preferences.ParseExisting("{}"); Assert.False(old.UseNearbyText); Assert.False(old.ShareNearbyText);
    }
    [Fact] public void SharingRequiresBothConsentsAndBoundedValidText()
    {
        Assert.Null(NearbyContext.ForSharing(false, true, "private context"));
        Assert.Null(NearbyContext.ForSharing(true, false, "private context"));
        Assert.Null(NearbyContext.ForSharing(true, true, new string('a', 257)));
        Assert.Null(NearbyContext.ForSharing(true, true, "secret\0text"));
        Assert.Null(NearbyContext.Validate("bad\uD800"));
        Assert.Equal("Hello 😀 there", NearbyContext.ForSharing(true, true, "Hello 😀 there"));
        Assert.True(NearbyContext.CanStreamDirect(true, true, false, false, null));
        Assert.False(NearbyContext.CanStreamDirect(true, true, false, false, "context"));
    }
    [Fact] public void LocallyMatchesOnlyAlreadyPresentWordsAndCase()
    {
        string context = "Talk with Prabhjot about FastAPI and Groq now.";
        Assert.Equal("Ask Prabhjot about FastAPI, Groq and unlisted words.", NearbyContext.ApplyCase("Ask prabhjot about fastapi, groq and unlisted words.", context));
        Assert.Equal("Ask prabjot.", NearbyContext.ApplyCase("Ask prabjot.", context));
        Assert.Equal("Speak to alice.", NearbyContext.ApplyCase("Speak to alice.", "Hello. Alice arrived today."));
        Assert.Equal("I like groq.", NearbyContext.ApplyCase("I like groq.", " use Groq then GROQ here "));
        Assert.Equal("Keep groq.", NearbyContext.ApplyCase("Keep groq.", "Groq"));
    }
}
