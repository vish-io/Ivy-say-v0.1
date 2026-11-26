using System.Text.RegularExpressions;

namespace IvySay;

// Cheap warning signals, not a semantic validator or an injection boundary.
// In particular, paraphrases and omitted/changed facts can evade these checks.
internal static class FormatterFidelity
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Negation = new(@"\b(?:not|never|cannot|can't|won't|don't|doesn't|didn't|isn't|aren't|wasn't|weren't|wouldn't|couldn't|shouldn't)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Correction = new(@"\b(?:sorry|scratch that|i mean|rather|instead|actually)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Diagnostic codes describe what a heuristic noticed, not what the model meant.
    public static string? SuspicionReason(string input, string output, string? mode)
    {
        if (string.IsNullOrWhiteSpace(output)) return "Empty output";
        var a = Words.Matches(input).Select(m => m.Value).ToArray();
        var b = Words.Matches(output).Select(m => m.Value).ToArray();
        if (a.Length == 0 || b.Length == 0) return null;
        if (DictationGuard.RequiresGuard(input) && !DictationGuard.IsConsistent(input, output)) return "Question or request changed";
        if (b.Length >= a.Length + 3 && a.Select((word, i) => string.Equals(word, b[i], StringComparison.OrdinalIgnoreCase)).All(x => x)) return "Possible added continuation";
        if (b.Length > Math.Max(12, a.Length * 2)) return "Unusually expanded output";
        // A missing negation may reflect a valid revision or a meaning change; we cannot distinguish them here.
        if (!Correction.IsMatch(input) && Negation.IsMatch(input) && !Negation.IsMatch(output)) return "Negation missing from output";
        return null;
    }

    public static bool IsSuspicious(string input, string output, string? mode) => SuspicionReason(input, output, mode) != null;

    // Only called before any output has been inserted. One retry, never an unbounded loop.
    public static async Task<(string? Text, bool Rejected, string? Reason)> CheckAsync(string input, string first, string? mode,
        Func<CancellationToken, Task<string>> retry, CancellationToken token)
    {
        string? firstReason = SuspicionReason(input, first, mode);
        if (firstReason == null) return (first, false, null);
        token.ThrowIfCancellationRequested();
        try
        {
            string second = await retry(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            string? secondReason = SuspicionReason(input, second, mode);
            return secondReason != null ? (null, true, $"First: {firstReason}; retry: {secondReason}") : (second, false, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return (null, true, $"First: {firstReason}; retry failed"); }
    }
}
