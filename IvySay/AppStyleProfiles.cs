using System.Text.RegularExpressions;

namespace IvySay;

// Only bounded aggregate counts are saved, never the observed text.
internal sealed class AppStyleProfile
{
    public string App { get; set; } = "";
    public int AddPeriod { get; set; }
    public int RemovePeriod { get; set; }
    public int ListsAsBullets { get; set; }
    public int ListsAsProse { get; set; }
    public AppStyleProfile Copy() => new() { App = App, AddPeriod = AddPeriod, RemovePeriod = RemovePeriod, ListsAsBullets = ListsAsBullets, ListsAsProse = ListsAsProse };
}
internal static class AppStyleProfiles
{
    private static readonly Regex OpeningCommand = new(@"^\s*(email|code|bullet|bullets)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SpokenLayout = new(@"\b(period|full stop|question mark|exclamation mark|new line|new paragraph|bullet point)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Bullet = new(@"^\s*[-*•]\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool Eligible(string transcript, string? mode) => FormattingModes.Normalize(mode) == "clean" && !OpeningCommand.IsMatch(transcript) && !SpokenLayout.IsMatch(transcript) && !DictationGuard.RequiresGuard(transcript);
    public static string? NormalizeApp(string? app) => AppContextData.Normalize(app)?.ToLowerInvariant();
    public static void Validate(List<AppStyleProfile>? profiles)
    {
        if (profiles == null || profiles.Count > 40) throw new ArgumentException("Invalid app profiles.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in profiles)
            if (p == null || NormalizeApp(p.App) != p.App || !seen.Add(p.App) || p.AddPeriod is < 0 or > 5 || p.RemovePeriod is < 0 or > 5 || p.ListsAsBullets is < 0 or > 5 || p.ListsAsProse is < 0 or > 5) throw new ArgumentException("Invalid app profile.");
    }
    private static int Signal(int positive, int negative) => positive >= 3 && positive - negative >= 2 ? 1 : negative >= 3 && negative - positive >= 2 ? -1 : 0;
    public static string? Hint(List<AppStyleProfile> profiles, string? app, string transcript, string? mode)
    {
        if (!Eligible(transcript, mode)) return null;
        string? key = NormalizeApp(app);
        var p = profiles.FirstOrDefault(x => x.App == key);
        if (p == null) return null;
        var pieces = new List<string>();
        switch (Signal(p.AddPeriod, p.RemovePeriod)) { case 1: pieces.Add("End ordinary statements with a period."); break; case -1: pieces.Add("Omit the final period on ordinary statements."); break; }
        switch (Signal(p.ListsAsBullets, p.ListsAsProse)) { case 1: pieces.Add("When a list is appropriate, prefer bullets."); break; case -1: pieces.Add("When a list is appropriate, prefer prose."); break; }
        return pieces.Count == 0 ? null : string.Join(" ", pieces);
    }
    public static string? SafeHint(string? hint)
    {
        string[] period = { "", "End ordinary statements with a period.", "Omit the final period on ordinary statements." };
        string[] list = { "", "When a list is appropriate, prefer bullets.", "When a list is appropriate, prefer prose." };
        return period.SelectMany(a => list.Select(b => (a + " " + b).Trim())).Any(x => x.Length > 0 && x == hint) ? hint : null;
    }
    public static bool Observe(List<AppStyleProfile> profiles, string? app, string inserted, string edited)
    {
        string? key = NormalizeApp(app);
        if (key == null || inserted.Length is 0 or > 160 || edited.Length is 0 or > 160 || inserted == edited) return false;
        string a = inserted.Trim(), b = edited.Trim();
        int period = 0, list = 0;
        if (a.Length > 1 && a.EndsWith('.') && a[..^1] == b && !a[..^1].EndsWith('.')) period = -1;
        else if (b.Length > 1 && b.EndsWith('.') && b[..^1] == a && !b[..^1].EndsWith('.')) period = 1;
        else
        {
            bool aList = IsBulletList(a), bList = IsBulletList(b);
            if (aList != bList && NormalizeList(a) == NormalizeList(b)) list = bList ? 1 : -1;
        }
        if (period == 0 && list == 0) return false;
        var p = profiles.FirstOrDefault(x => x.App == key);
        if (p == null) { if (profiles.Count >= 40) return false; profiles.Add(p = new AppStyleProfile { App = key }); }
        if (period == 1) { p.AddPeriod = Math.Min(5, p.AddPeriod + 1); p.RemovePeriod = Math.Max(0, p.RemovePeriod - 1); }
        if (period == -1) { p.RemovePeriod = Math.Min(5, p.RemovePeriod + 1); p.AddPeriod = Math.Max(0, p.AddPeriod - 1); }
        if (list == 1) { p.ListsAsBullets = Math.Min(5, p.ListsAsBullets + 1); p.ListsAsProse = Math.Max(0, p.ListsAsProse - 1); }
        if (list == -1) { p.ListsAsProse = Math.Min(5, p.ListsAsProse + 1); p.ListsAsBullets = Math.Max(0, p.ListsAsBullets - 1); }
        return true;
    }
    private static bool IsBulletList(string s)
    {
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length >= 2 && lines.All(line => Bullet.IsMatch(line));
    }
    private static string NormalizeList(string s) => Whitespace.Replace(string.Join(" ", s.Split('\n').Select(line => Bullet.Replace(line, ""))), " ").Trim();
    public static string Summary(AppStyleProfile p)
    {
        var hints = Hint(new() { p }, p.App, "ordinary statement", "clean");
        return p.App + " — " + (hints ?? "Learning (more examples needed)");
    }
}
