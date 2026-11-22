using System.Net;
using System.Net.Http;
using IvySay;
using Xunit;
public class FailureDetailsTests
{
    [Fact] public void ReportsStageAndHttpCodeWithoutSensitiveProviderMessage()
    {
        var text = FailureDetails.Explain(new HttpRequestException("secret transcript", null, HttpStatusCode.TooManyRequests), "Transcription");
        Assert.Contains("429", text); Assert.Contains("Transcription", text); Assert.DoesNotContain("secret", text);
    }
    [Fact] public void ReportsMicrophoneTimeout()
    {
        Assert.Contains("Microphone", FailureDetails.Explain(new TimeoutException(), "Microphone"));
    }
    [Fact] public void DoesNotShowUnknownExceptionText()
    {
        Assert.DoesNotContain("private", FailureDetails.Explain(new InvalidOperationException("private"), "Setup"));
    }
}
