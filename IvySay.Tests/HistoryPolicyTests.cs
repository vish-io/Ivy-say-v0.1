using IvySay;
using Xunit;

public class HistoryPolicyTests
{
    [Fact] public void HistoryAndAudioAreOffByDefaultForOldAndNewPreferences()
    {
        foreach (var p in new[] { new Preferences(), Preferences.ParseExisting("{}") })
        { Assert.False(p.HistoryEnabled); Assert.False(p.HistoryAudioEnabled); Assert.Equal(30, p.HistoryDays); }
        Assert.Equal(30, Preferences.ParseExisting("{\"HistoryDays\":999}").HistoryDays);
    }
    [Fact] public void RetentionBoundsAndEligibility()
    {
        var now = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        Assert.False(HistoryPolicy.Expired(now.AddDays(-7), now, 7));
        Assert.True(HistoryPolicy.Expired(now.AddDays(-7).AddSeconds(-1), now, 7));
        Assert.True(HistoryPolicy.Expired(now.AddMinutes(6), now, 30));
        Assert.False(HistoryPolicy.Eligible("", "output", null));
        Assert.False(HistoryPolicy.Eligible("raw", "", null));
        Assert.False(HistoryPolicy.Eligible(new string('a', HistoryPolicy.MaxTextChars + 1), "out", null));
        Assert.False(HistoryPolicy.Eligible("raw", "out", new byte[43]));
        Assert.False(HistoryPolicy.Eligible("raw", "out", new byte[HistoryPolicy.MaxWavBytes + 1]));
        Assert.True(HistoryPolicy.Eligible("raw", "out", null));
        Assert.True(HistoryPolicy.Eligible("raw", "out", new byte[44]));
    }
}
