namespace IvySay;

// Exact whole-utterance triggers only. No fuzzy matching or regex replacement.
internal sealed class SnippetRules
{
    private readonly Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);
    public static SnippetRules Parse(string text)
    {
        var result = new SnippetRules();
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int at = line.IndexOf("=>", StringComparison.Ordinal);
            if (at < 0) throw new ArgumentException("Each snippet must use the format spoken phrase => text.");
            string trigger = Normalize(line[..at]);
            string value = line[(at + 2)..].Trim();
            if (trigger.Length is 0 or > 80 || value.Length is 0 or > 300 || trigger.Any(char.IsControl) || value.Any(char.IsControl))
                throw new ArgumentException("Snippet phrases must be at most 80 characters and expansions at most 300 characters on one line.");
            if (!result.entries.TryAdd(trigger, value)) throw new ArgumentException("Each snippet phrase must be unique.");
            if (result.entries.Count > 25) throw new ArgumentException("Use at most 25 snippets.");
        }
        return result;
    }
    private static string Normalize(string text) => text.Trim().TrimEnd('.', ',', '!', '?').TrimEnd();
    public bool TryMatch(string transcript, out string value) => entries.TryGetValue(Normalize(transcript), out value!);
}
