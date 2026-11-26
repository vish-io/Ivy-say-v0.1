using IvySay;
using Xunit;

public class FormatterFidelityTests
{
    [Theory]
    [InlineData("If you had to make this desktop app to an APK how many days will you take", "If you had to make this desktop app to an APK, how many days will you take? I will take about one week.")]
    [InlineData("please send the report", "Please send the report. Here is my detailed explanation of the task and all the steps you should follow.")]
    [InlineData("I do not think it is ready", "I think it is ready.")]
    [InlineData("what day is it", "Today is Monday")]
    public void FlagsExamplesOfPotentialMeaningChanges(string input, string output) => Assert.True(FormatterFidelity.IsSuspicious(input, output, "clean"));

    [Theory]
    [InlineData("If you had to make this desktop app to an APK how many days will you take", "If you had to make this desktop app into an APK, how many days would you take?")]
    [InlineData("send the report Tuesday no sorry Thursday", "Send the report Thursday.")]
    [InlineData("she said no no no", "She said no, no, no.")]
    [InlineData("I do not think it is ready", "I don't think it's ready.")]
    [InlineData("ask Neha about build 1.15", "Ask Neha about build 1.15.")]
    public void AcceptsCommonFaithfulEdits(string input, string output) => Assert.False(FormatterFidelity.IsSuspicious(input, output, "clean"));

    [Fact] public async Task NoExtraCallForAcceptedOutput()
    {
        var result = await FormatterFidelity.CheckAsync("send the report", "Send the report.", "clean", _ => throw new Exception("unexpected retry"), CancellationToken.None);
        Assert.Equal("Send the report.", result.Text); Assert.False(result.Rejected);
    }
    [Fact] public async Task RetriesOnceAndAcceptsValidResult()
    {
        int calls = 0;
        var result = await FormatterFidelity.CheckAsync("what day is it", "Today is Monday", "clean", _ => { calls++; return Task.FromResult("What day is it?"); }, CancellationToken.None);
        Assert.Equal(1, calls); Assert.False(result.Rejected); Assert.Equal("What day is it?", result.Text);
    }
    [Fact] public async Task FailedOrInvalidRetryPreservesTranscript()
    {
        var invalid = await FormatterFidelity.CheckAsync("what day is it", "Today is Monday", "clean", _ => Task.FromResult("It is Tuesday"), CancellationToken.None);
        Assert.True(invalid.Rejected); Assert.Null(invalid.Text);
        var failed = await FormatterFidelity.CheckAsync("what day is it", "Today is Monday", "clean", _ => throw new HttpRequestException(), CancellationToken.None);
        Assert.True(failed.Rejected); Assert.Null(failed.Text);
    }
    [Fact] public async Task ReportsNegationFlagAndAcceptsAConservativeRetry()
    {
        const string input = "Invite the team Thursday. Thursday won't work, so move it to Friday.";
        const string first = "Invite the team Friday.";
        const string second = "Invite the team Friday because Thursday won't work.";
        Assert.Equal("Negation missing from output", FormatterFidelity.SuspicionReason(input, first, "clean"));
        var accepted = await FormatterFidelity.CheckAsync(input, first, "clean", _ => Task.FromResult(second), CancellationToken.None);
        Assert.False(accepted.Rejected); Assert.Equal(second, accepted.Text);
        var rejected = await FormatterFidelity.CheckAsync(input, first, "clean", _ => Task.FromResult(first), CancellationToken.None);
        Assert.True(rejected.Rejected); Assert.Contains("Negation missing from output", rejected.Reason);
    }
    [Fact] public void NegationGuardRemainsInPlaceForDangerousOmissions()
    {
        Assert.Equal("Negation missing from output", FormatterFidelity.SuspicionReason("Do not send the money", "Send the money.", "clean"));
    }
    [Fact] public async Task CancellationIsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FormatterFidelity.CheckAsync("what day is it", "Monday", "clean", _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); }, cts.Token));
    }
}
