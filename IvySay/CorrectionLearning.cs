using System.Text.RegularExpressions;

namespace IvySay;

// Deliberately narrow: only a single small spelling edit in otherwise unchanged output.
internal static class CorrectionLearning
{
    private static readonly Regex Word = new(@"^[\p{L}\p{M}]{4,32}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Tokens = new(@"[\p{L}\p{M}]+|[^\p{L}\p{M}]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string? Candidate(string inserted, string edited)
    {
        if (inserted.Length is 0 or > 160 || edited.Length is 0 or > 160 || inserted == edited) return null;
        var a = Tokens.Matches(inserted).Select(m => m.Value).ToArray();
        var b = Tokens.Matches(edited).Select(m => m.Value).ToArray();
        if (a.Length != b.Length) return null;
        string? candidate = null;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            if (candidate != null || !Word.IsMatch(a[i]) || !Word.IsMatch(b[i]) ||
                string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase) || Distance(a[i], b[i]) > 2) return null;
            candidate = b[i];
        }
        return candidate;
    }
    private static int Distance(string a, string b)
    {
        a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
        int[] previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            int[] next = new int[b.Length + 1]; next[0] = i;
            for (int j = 1; j <= b.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = next;
        }
        return previous[b.Length];
    }
    public static string Add(string existing, string candidate)
    {
        if (!Word.IsMatch(candidate)) return existing;
        var items = existing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (items.Any(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase)) || items.Count >= 100) return existing;
        string result = string.Join('\n', items.Append(candidate));
        return result.Length <= 1000 ? result : existing;
    }
    public static string? Extract(string text, string before, string after, bool atStart, bool atEnd, bool reachesStart, bool reachesEnd)
    {
        if ((!atStart && before.Length < 12) || (!atEnd && after.Length < 12) || (atStart && !reachesStart) || (atEnd && !reachesEnd)) return null;
        int prefix = atStart ? (text.StartsWith(before, StringComparison.Ordinal) ? 0 : -1) : text.IndexOf(before, StringComparison.Ordinal);
        if (prefix < 0 || (!atStart && text.IndexOf(before, prefix + 1, StringComparison.Ordinal) >= 0)) return null;
        int start = prefix + before.Length;
        int end = atEnd ? (text.EndsWith(after, StringComparison.Ordinal) ? text.Length - after.Length : -1) : text.IndexOf(after, start, StringComparison.Ordinal);
        if (end < start || (!atEnd && text.IndexOf(after, end + 1, StringComparison.Ordinal) >= 0) || end - start > 160) return null;
        return text[start..end];
    }
}
