using IvySay;
using Xunit;
public class QuickNotesAndModesTests
{
    [Fact] public void ModesAreWhitelistedAndCycle()
    {
        Assert.Equal("clean", FormattingModes.Normalize("other"));
        Assert.Equal("concise", FormattingModes.Next("clean"));
        Assert.Equal("structured", FormattingModes.Next("concise"));
        Assert.Equal("clean", FormattingModes.Next("structured"));
        var p = new Preferences(); Assert.False(p.QuickNotesEnabled); Assert.True(p.StatusSounds);
    }
    [Fact] public async Task NotesAppendToExplicitFolderAndRejectMissingFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ivy-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            await QuickNotes.AppendAsync(folder, "first", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), CancellationToken.None);
            await QuickNotes.AppendAsync(folder, "second", new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero), CancellationToken.None);
            string text = await File.ReadAllTextAsync(Path.Combine(folder, "inbox.md"));
            Assert.Contains("2026-01-02 03:04:05 +00:00", text); Assert.Contains("first", text); Assert.Contains("second", text);
            await Assert.ThrowsAsync<IOException>(() => QuickNotes.AppendAsync(Path.Combine(folder, "missing"), "x", DateTimeOffset.Now, CancellationToken.None));
        }
        finally { Directory.Delete(folder, true); }
    }
}
