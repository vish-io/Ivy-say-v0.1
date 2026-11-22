using System.Text.RegularExpressions;
namespace IvySay;
// A conservative heuristic, not a prompt-injection detector or proof of semantic fidelity.
internal static class DictationGuard
{
    private static readonly Regex Risk = new(@"\?|\b(what|who|where|when|why|how|which|can you|could you|would you|tell me|answer|ignore|system prompt|previous instructions|reveal|translate|write (a|me) (program|code|poem|story))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly HashSet<string> Stops = new(StringComparer.OrdinalIgnoreCase) { "a", "an", "the", "is", "it", "to", "of", "and", "or", "in", "on", "do", "you", "me", "please" };
    public static bool RequiresGuard(string text) => Risk.IsMatch(text);
    public static bool IsConsistent(string input, string output)
    {
        var words = Words.Matches(input).Select(m => m.Value).Where(w => !Stops.Contains(w)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var result = Words.Matches(output).Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (words.Length == 0) return false;
        int kept = words.Count(result.Contains);
        return kept * 4 >= words.Length * 3 && result.Count <= Math.Max(8, words.Length * 3);
    }
    public static string SafeText(string input)
    {
        string value = input.Trim();
        if (value.Length == 0) return value;
        if (char.IsLower(value[0])) value = char.ToUpper(value[0], System.Globalization.CultureInfo.CurrentCulture) + value[1..];
        if (!".?!".Contains(value[^1])) value += Risk.IsMatch(value) && Regex.IsMatch(value, @"^\s*(what|who|where|when|why|how|which|can|could|would)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ? "?" : ".";
        return value;
    }
}
