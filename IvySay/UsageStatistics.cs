using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IvySay;

// Only daily aggregate counts and recording durations are persisted, never dictated text.
internal sealed class UsageStatistics(string path)
{
    private sealed class DailyUsage { public DailyUsage() { } public string Date { get; set; } = ""; public long Words { get; set; } public long Milliseconds { get; set; } }
    private static readonly Regex Words = new(@"[\p{L}\p{M}\p{N}]+(?:['’\-][\p{L}\p{M}\p{N}]+)*", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly object gate = new();
    private Task pending = Task.CompletedTask;
    public static UsageStatistics Default { get; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "usage-statistics.json"));
    public Task RecordAsync(string result, TimeSpan recordingDuration, DateTime date)
    {
        lock (gate)
        {
            // Serialize background writes; never block text insertion on statistics I/O.
            pending = pending.ContinueWith(_ =>
            {
                try { RecordCore(result, recordingDuration, date.Date); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or RegexMatchTimeoutException or ArgumentException or OverflowException) { }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return pending;
        }
    }
    private List<DailyUsage> Read()
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 32768) throw new InvalidDataException("Statistics file is too large.");
        return JsonSerializer.Deserialize<List<DailyUsage>>(File.ReadAllText(path)) ?? new();
    }
    private void RecordCore(string result, TimeSpan duration, DateTime date)
    {
        int words = Words.Matches(result).Count;
        long milliseconds = (long)duration.TotalMilliseconds;
        if (words == 0 || milliseconds <= 0 || milliseconds > 120_000) return;
        var entries = Read().Where(x => DateTime.TryParseExact(x.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day >= date.AddDays(-6) && day <= date && x.Words >= 0 && x.Milliseconds > 0).ToList();
        string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var entry = entries.FirstOrDefault(x => x.Date == key);
        if (entry == null) { entry = new DailyUsage { Date = key }; entries.Add(entry); }
        entry.Words = checked(entry.Words + words);
        entry.Milliseconds = checked(entry.Milliseconds + milliseconds);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        string temp = path + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(entries)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void WaitForPending() { Task task; lock (gate) task = pending; task.GetAwaiter().GetResult(); }
    public double? SevenDayAverage(DateTime today)
    {
        WaitForPending();
        var start = today.Date.AddDays(-6);
        var selected = Read().Where(x => DateTime.TryParseExact(x.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day >= start && day <= today.Date && x.Words >= 0 && x.Milliseconds > 0).ToList();
        double seconds = selected.Sum(x => (double)x.Milliseconds) / 1000d;
        return seconds > 0 ? selected.Sum(x => (double)x.Words) * 60d / seconds : null;
    }
    public void Clear() { WaitForPending(); if (File.Exists(path)) File.Delete(path); }
}
