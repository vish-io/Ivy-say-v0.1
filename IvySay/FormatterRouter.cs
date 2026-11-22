using System.IO;
using System.Net;
using System.Net.Http;

namespace IvySay;

// Retries only before direct output can have reached the target; never retries indefinitely.
internal static class FormatterRouter
{
    public const string Primary = "openai/gpt-oss-20b";
    public const string Fallback = "openai/gpt-oss-120b";
    public const string LastResort = "qwen/qwen3.8-27b";
    public static string OtherModel(string selected) => selected switch
    {
        Primary => Fallback,
        Fallback => Primary,
        _ => throw new ArgumentException("Unsupported formatter model.", nameof(selected))
    };
    public static async Task<string> RunAsync(
        Func<string, Func<string, Task>, CancellationToken, Task<string>> invoke,
        Func<string, Task> onDelta,
        Action onFallback,
        bool directTyping,
        CancellationToken token,
        string initialModel = Primary, Action<string>? onModelChange = null)
    {
        string alternateModel = OtherModel(initialModel);
        bool directOutputPossible = false;
        async Task Forward(string text)
        {
            // Mark before the callback: SendInput may partially succeed and then throw.
            if (directTyping && text.Length > 0) directOutputPossible = true;
            await onDelta(text);
        }
        string[] sequence = { initialModel, alternateModel, LastResort };
        for (int i = 0; ; i++)
        {
            try { return await invoke(sequence[i], Forward, token); }
            catch (Exception ex) when (i < sequence.Length - 1 && !token.IsCancellationRequested && !directOutputPossible && IsRetryable(ex))
            {
                token.ThrowIfCancellationRequested();
                onFallback();
                onModelChange?.Invoke(sequence[i + 1]);
            }
        }
    }
    private static bool IsRetryable(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.NotFound or HttpStatusCode.RequestTimeout } => true,
        HttpRequestException { StatusCode: { } code } when (int)code >= 500 && (int)code <= 599 => true,
        OperationCanceledException => true, // caller cancellation is excluded in the catch filter
        IOException => true,
        _ => false
    };
}
