namespace IvySay;

internal static class HistoryPolicy
{
    public const int MaxEntries = 100;
    public const long MaxBytes = 64L * 1024 * 1024;
    public const int MaxWavBytes = 120 * 32000 + 44;
    public const int MaxTextChars = 65536;
    public static int Days(int days) => days is 7 or 30 or 90 ? days : 30;
    public static bool Expired(DateTimeOffset created, DateTimeOffset now, int days) => created < now.AddDays(-Days(days)) || created > now.AddMinutes(5);
    public static bool Eligible(string? raw, string? result, byte[]? audio) =>
        !string.IsNullOrWhiteSpace(raw) && raw.Length <= MaxTextChars && !string.IsNullOrWhiteSpace(result) && result.Length <= MaxTextChars &&
        (audio == null || (audio.Length >= 44 && audio.Length <= MaxWavBytes));
}
