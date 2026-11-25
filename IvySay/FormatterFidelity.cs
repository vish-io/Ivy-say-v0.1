using System.Text.RegularExpressions;

namespace IvySay;

// Cheap warning signals, not a semantic validator or an injection boundary.
// In particular, paraphrases and omitted/changed facts can evade these checks.
internal static class FormatterFidelity
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Negation = new(@"\b(?:not|never|cannot|can't|won't|don't|doesn't|didn't|isn't|aren't|wasn't|weren't|wouldn't|couldn't|shouldn't)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Correction = new(@"\b(?:sorry|scratch that|i mean|rather|instead|actually)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static bool IsSuspicious(string input, string output, string? mode)
    {
        if (string.IsNullOrWhiteSpace(output)) return true;
        var a = Words.Matches(input).Select(m => m.Value).ToArray();
        var b = Words.Matches(output).Select(m => m.Value).ToArray();
        if (a.Length == 0 || b.Length == 0) return false;
        if (DictationGuard.RequiresGuard(input) && !DictationGuard.IsConsistent(input, output)) return true;
        // Exact dictated text followed by a substantial addition can be an appended answer.
        // Do not infer that arbitrary rewordings are safe just because this did not match.
        if (b.Length >= a.Length + 3 && a.Select((word, i) => string.Equals(word, b[i], StringComparison.OrdinalIgnoreCase)).All(x => x)) return true;
        // Very large expansions are unusual for transcript editing.
        if (b.Length > Math.Max(12, a.Length * 2)) return true;
        // Only flag complete removal of explicit negation, not rewording or corrections.
        if (!Correction.IsMatch(input) && Negation.IsMatch(input) && !Negation.IsMatch(output)) return true;
        return false;
    }

    // Only called before any output has been inserted. One retry, never an unbounded loop.
    public static async Task<(string? Text, bool Rejected)> CheckAsync(string input, string first, string? mode,
        Func<CancellationToken, Task<string>> retry, CancellationToken token)
    {
        if (!IsSuspicious(input, first, mode)) return (first, false);
        token.ThrowIfCancellationRequested();
        try
        {
            string second = await retry(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return IsSuspicious(input, second, mode) ? (null, true) : (second, false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return (null, true); }
    }
}
