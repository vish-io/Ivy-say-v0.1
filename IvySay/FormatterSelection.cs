using System.Text.RegularExpressions;

namespace IvySay;

internal readonly record struct FormatterChoice(string Model, string Reason);

// Routing is local and heuristic: it does not claim to assess meaning or correctness.
internal static class FormatterSelection
{
    public const int DefaultWordThreshold = 120;
    public const int MinWordThreshold = 20;
    public const int MaxWordThreshold = 500;
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;
    private static readonly Regex Words = new(@"[\p{L}\p{M}\p{N}]+(?:['’\-][\p{L}\p{M}\p{N}]+)*", Options);
    // Deliberately limited to explicit English phrases. A plain 'no', 'like', or
    // 'instead' is not enough: they can be ordinary dictated content.
    private static readonly Regex CorrectionCue = new(
        @"\b(?:i\s+mean|change\s+that|actually\s*[,;:]?\s*(?:change|make|use|set|choose|replace|\d)|(?:no(?:[\s,;:.!?—–-]+no){0,5}|sorry)[\s,;:.!?—–-]+(?:i\s+mean|use|make|do|change|set|choose|replace))\b", Options);

    public static FormatterChoice Select(string rawTranscript, bool enabled, int wordThreshold)
    {
        if (!enabled) return new(FormatterRouter.Primary, "smart routing off");
        int threshold = wordThreshold is >= MinWordThreshold and <= MaxWordThreshold ? wordThreshold : DefaultWordThreshold;
        // Use the original transcript so user corrections do not erase correction cues.
        if (CorrectionCue.IsMatch(rawTranscript)) return new(FormatterRouter.Fallback, "correction cue");
        int count = 0;
        foreach (var _ in Words.EnumerateMatches(rawTranscript))
            if (++count >= threshold) return new(FormatterRouter.Fallback, "long dictation");
        return new(FormatterRouter.Primary, "standard dictation");
    }
}
