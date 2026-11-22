using System.Net.Http;

namespace IvySay;
// Sanitized diagnostics only: never display exception messages from the provider or audio driver.
internal static class FailureDetails
{
    public static string Explain(Exception error, string stage)
    {
        if (error is HttpRequestException { StatusCode: not null } http)
            return $"{stage}: Groq returned HTTP {(int)http.StatusCode.Value}. Check your API key, account limits, and network connection.";
        if (error is HttpRequestException transport)
        {
            string cause = transport.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "The Groq server name could not be resolved. Check DNS, VPN or network settings.",
                HttpRequestError.SecureConnectionError => "A secure connection could not be established. Check your system clock, VPN or network security settings.",
                HttpRequestError.ProxyTunnelError => "The network proxy could not connect. Check your proxy or VPN settings.",
                _ => "Could not reach Groq. Check your internet, VPN or firewall."
            };
            return $"{stage}: {cause} After reconnecting, start a new dictation; you do not need to restart Ivy Say.";
        }
        if (error is TimeoutException or OperationCanceledException)
            return stage == "Microphone" ? "Microphone: No audio arrived in time. Check Windows microphone permissions and the selected device, then try again." : $"{stage}: The request timed out. Check your connection and try again.";
        if (error is InvalidOperationException && SafeMessage(error.Message)) return $"{stage}: {error.Message}";
        return $"{stage}: Could not complete. Check your microphone, key, and network connection, then try again.";
    }
    private static bool SafeMessage(string message) => message is
        "Focus the app where you want to type first." or
        "Click an editable text box before starting dictation. The API key field cannot be used for dictation." or
        "The selected microphone is unavailable. Choose another in settings." or
        "Multiple microphones have the same Windows recording name. Ivy Say cannot safely identify the selected device. Set the intended microphone as the Windows default, then select Windows default in Ivy Say." or
        "Microphone stopped before recording began. Check the selected input and Windows permissions." or
        "Microphone recording failed." or
        "Almost no microphone signal was captured. Check the selected input, mute, Windows microphone permissions and input volume, then try again." or
        "Recording exceeded the two-minute limit. Please try a shorter dictation." or
        "Recording was too short. Please try again.";
}
