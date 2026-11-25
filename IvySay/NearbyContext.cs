using System.Text.RegularExpressions;
using System.Xml;

namespace IvySay;

// Nearby text is temporary untrusted data. It is never used as an instruction or stored.
internal static class NearbyContext
{
    public const int MaxCharacters = 256;
    private static readonly Regex Words = new(@"(?<![\p{L}\p{M}\p{N}_])[\p{L}][\p{L}\p{M}\p{N}]{3,39}(?![\p{L}\p{M}\p{N}_])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string? Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxCharacters) return null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c)) { if (i + 1 >= text.Length || !char.IsLowSurrogate(text[++i])) return null; }
            else if (char.IsLowSurrogate(c) || !XmlConvert.IsXmlChar(c)) return null;
        }
        return text;
    }
    public static string? ForSharing(bool enabled, bool share, string? text) => enabled && share ? Validate(text) : null;
    public static bool CanStreamDirect(bool requested, bool unsafeAllowed, bool quickNote, bool guarded, string? shared) => requested && unsafeAllowed && !quickNote && !guarded && shared == null;
    // Only correct casing of already-dictated lowercase words. Never add, replace, or infer words.
    public static string ApplyCase(string output, string? context)
    {
        if (Validate(context) == null || output.Length > 4096) return output;
        try
        {
        var candidates = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Words.Matches(context!))
        {
            if (match.Index == 0 || match.Index + match.Length == context!.Length || !match.Value.Any(char.IsUpper)) continue;
            int prev = match.Index - 1;
            while (prev >= 0 && char.IsWhiteSpace(context[prev]) && context[prev] != '\n' && context[prev] != '\r') prev--;
            if (prev < 0 || ".!?\r\n".Contains(context[prev])) continue; // Sentence-initial capitalization is ambiguous.
            if (candidates.TryGetValue(match.Value, out string? old) && old != match.Value) candidates[match.Value] = null;
            else if (!candidates.ContainsKey(match.Value)) candidates[match.Value] = match.Value;
        }
        return Words.Replace(output, m => m.Value == m.Value.ToLowerInvariant() && candidates.TryGetValue(m.Value, out string? proper) && proper != null ? proper : m.Value);
        }
        catch (RegexMatchTimeoutException) { return output; }
    }
}
