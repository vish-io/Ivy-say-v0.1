using System.Text.RegularExpressions;

namespace IvySay;

// Explicit user-authored rules only; no automatic learning or fuzzy substitutions.
internal sealed class PersonalizationRules
{
    private readonly Dictionary<string, string> replacements;
    private readonly Regex? matcher;
    private PersonalizationRules(Dictionary<string, string> replacements)
    {
        this.replacements = replacements;
        if (replacements.Count > 0)
        {
            string choices = string.Join("|", replacements.Keys.OrderByDescending(x => x.Length).Select(Regex.Escape));
            matcher = new Regex($@"(?<![\p{{L}}\p{{M}}\p{{N}}_])(?:{choices})(?![\p{{L}}\p{{M}}\p{{N}}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
    }
    public static PersonalizationRules Parse(string text)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split("=>", StringSplitOptions.None);
            if (parts.Length != 2) throw new ArgumentException("Each correction must use the format heard => intended.");
            string from = parts[0].Trim(), to = parts[1].Trim();
            if (from.Length is 0 or > 80 || to.Length is 0 or > 80 || from.Any(char.IsControl) || to.Any(char.IsControl))
                throw new ArgumentException("Corrections must have a heard phrase and intended phrase of at most 80 characters each.");
            if (!entries.TryAdd(from, to)) throw new ArgumentException("Each heard phrase must be unique.");
            if (entries.Count > 25) throw new ArgumentException("Use at most 25 corrections.");
        }
        return new PersonalizationRules(entries);
    }
    public string Apply(string transcript) => matcher == null ? transcript : matcher.Replace(transcript, match => replacements.TryGetValue(match.Value, out string? value) ? value : match.Value);

    // Prioritize whole vocabulary entries, then existing spelling hints; never cut a term mid-word.
    public static string BuildPrompt(string vocabulary, string hints)
    {
        var terms = vocabulary.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(hints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var selected = new List<string>();
        int length = 0;
        foreach (string term in terms)
        {
            int extra = term.Length + (selected.Count == 0 ? 0 : 2);
            if (length + extra > 160) continue;
            selected.Add(term); length += extra;
        }
        return string.Join(", ", selected);
    }
}
