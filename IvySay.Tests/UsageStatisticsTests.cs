using IvySay;
using Xunit;

public class UsageStatisticsTests
{
    [Fact]
    public async Task UsesWeightedLastSevenCalendarDaysWithoutPersistingDictation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "IvySayTests-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory, "usage.json");
        try
        {
            var stats = new UsageStatistics(file);
            DateTime today = new(2026, 11, 19);
            Assert.Null(stats.SevenDayAverage(today));
            await stats.RecordAsync("old secret words", TimeSpan.FromSeconds(60), today.AddDays(-7));
            await stats.RecordAsync("one two three four", TimeSpan.FromSeconds(30), today.AddDays(-6));
            await stats.RecordAsync("five six", TimeSpan.FromSeconds(30), today);
            Assert.Equal(6d, stats.SevenDayAverage(today)!.Value, 4);
            string stored = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain("one", stored);
            Assert.DoesNotContain("secret", stored);
            Assert.Contains("Words", stored);
            stats.Clear();
            Assert.False(File.Exists(file));
            Assert.Null(stats.SevenDayAverage(today));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task IgnoresEmptyAndInvalidDurations()
    {
        string directory = Path.Combine(Path.GetTempPath(), "IvySayTests-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory, "usage.json");
        try
        {
            var stats = new UsageStatistics(file);
            DateTime today = new(2026, 11, 19);
            await stats.RecordAsync("hello", TimeSpan.Zero, today);
            await stats.RecordAsync("...", TimeSpan.FromSeconds(10), today);
            Assert.Null(stats.SevenDayAverage(today));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task ConcurrentRecordsAreSerialized()
    {
        string directory = Path.Combine(Path.GetTempPath(), "IvySayTests-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory, "usage.json");
        try
        {
            var stats = new UsageStatistics(file);
            DateTime today = new(2026, 11, 19);
            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => stats.RecordAsync("one two", TimeSpan.FromSeconds(10), today)));
            Assert.Equal(12d, stats.SevenDayAverage(today)!.Value, 4);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
