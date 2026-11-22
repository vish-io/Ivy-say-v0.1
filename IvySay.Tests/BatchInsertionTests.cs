using IvySay;
using Xunit;
public class BatchInsertionTests
{
    [Fact] public void CompleteTextIsSubmittedOnceIncludingSurrogatesAndNewlines()
    {
        string text = "Hi 😀\nਸਤ ਸ੍ਰੀ ਅਕਾਲ"; int calls = 0, checks = 0; string accepted = "";
        BatchInsertion.Send(text, () => checks++, value => { calls++; Assert.Equal(text, value); return (uint)value.Length * 2; }, c => accepted += c);
        Assert.Equal(1, calls); Assert.Equal(1, checks); Assert.Equal(text, accepted);
    }
    [Fact] public void PartialSubmissionIsNotRetriedAndDoesNotReportFullAcceptance()
    {
        int calls = 0, observed = 0;
        var error = Assert.Throws<InvalidOperationException>(() => BatchInsertion.Send("hello", () => { }, _ => { calls++; return 3; }, _ => observed++));
        Assert.Contains("Some text may have been inserted", error.Message); Assert.Equal(1, calls); Assert.Equal(0, observed);
    }
    [Fact] public void FailedFocusValidationPreventsSubmission()
    {
        int calls = 0;
        Assert.Throws<InvalidOperationException>(() => BatchInsertion.Send("hello", () => throw new InvalidOperationException(), _ => { calls++; return 0; }));
        Assert.Equal(0, calls);
    }
}
