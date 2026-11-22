using System.IO;
using System.Text;
namespace IvySay;
internal static class QuickNotes
{
    public static async Task AppendAsync(string directory, string text, DateTimeOffset time, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new IOException("The selected notes folder is unavailable.");
        string entry = $"\n## {time:yyyy-MM-dd HH:mm:ss zzz}\n\n{text}\n";
        // A failed append may have partially written; callers must not automatically retry.
        await File.AppendAllTextAsync(Path.Combine(directory, "inbox.md"), entry, new UTF8Encoding(false), token);
    }
}
