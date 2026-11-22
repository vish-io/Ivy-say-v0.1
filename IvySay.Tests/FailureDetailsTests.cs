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
    [Fact] public void PreservesHelpfulSilenceDiagnostic()
    {
        const string message = "Almost no microphone signal was captured. Check the selected input, mute, Windows microphone permissions and input volume, then try again.";
        Assert.Contains(message, FailureDetails.Explain(new InvalidOperationException(message), "Transcription"));
    }
    [Fact] public void DoesNotShowUnknownExceptionText()
    {
        Assert.DoesNotContain("private", FailureDetails.Explain(new InvalidOperationException("private"), "Setup"));
    }
    [Fact] public void ReportsDnsWithoutLeakingExceptionText()
    {
        var error = new HttpRequestException(HttpRequestError.NameResolutionError, "private key and transcript", null);
        string message = FailureDetails.Explain(error, "Transcription");
        Assert.Contains("DNS", message); Assert.Contains("restart", message); Assert.DoesNotContain("private", message);
    }
}
