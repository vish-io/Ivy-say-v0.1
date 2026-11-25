using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace IvySay;

internal sealed class HistoryEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string Raw { get; set; } = "";
    public string Result { get; set; } = "";
    public byte[]? Audio { get; set; }
    public bool HasAudio { get; set; }
}

// Each file is DPAPI encrypted before reaching disk. All operations serialize; invalidation
// prevents a queued recording from reappearing after clear/disable.
internal static class DictationHistory
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "history");
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object CommitGate = new();
    private static long epoch;
    private const long MaxFile = 6 * 1024 * 1024;
    private static string PathFor(Guid id) => Path.Combine(Dir, id.ToString("N") + ".dat");
    public static void Invalidate() { lock (CommitGate) epoch++; }
    public static long Generation { get { lock (CommitGate) return epoch; } }
    private static HistoryEntry Read(string path)
    {
        if (new FileInfo(path).Length > MaxFile) throw new InvalidDataException("History entry is too large.");
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try
        {
            var e = JsonSerializer.Deserialize<HistoryEntry>(plain) ?? throw new InvalidDataException();
            if (e.Id == Guid.Empty || !string.Equals(Path.GetFileName(path), e.Id.ToString("N") + ".dat", StringComparison.OrdinalIgnoreCase) ||
                !HistoryPolicy.Eligible(e.Raw, e.Result, e.Audio) || e.HasAudio != (e.Audio != null)) throw new InvalidDataException("Invalid history entry.");
            return e;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    private static byte[] Encrypt(HistoryEntry entry)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(entry);
        try { return ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    private static void AtomicWrite(string path, byte[] encrypted)
    {
        Directory.CreateDirectory(Dir);
        string temp = path + ".tmp";
        try { File.WriteAllBytes(temp, encrypted); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static List<(HistoryEntry Entry, long Bytes)> ReadAll()
    {
        var items = new List<(HistoryEntry, long)>();
        if (!Directory.Exists(Dir)) return items;
        foreach (string temp in Directory.GetFiles(Dir, "*.tmp")) File.Delete(temp);
        string[] files = Directory.GetFiles(Dir, "*.dat");
        if (files.Length > HistoryPolicy.MaxEntries + 1) throw new InvalidDataException("Too many history files.");
        foreach (string path in files)
        {
            var entry = Read(path);
            if (entry.Audio != null) { CryptographicOperations.ZeroMemory(entry.Audio); entry.Audio = null; }
            items.Add((entry, new FileInfo(path).Length));
        }
        return items;
    }
    private static List<(HistoryEntry Entry, long Bytes)> Prune(int days)
    {
        var items = ReadAll(); var now = DateTimeOffset.UtcNow;
        foreach (var item in items.Where(x => HistoryPolicy.Expired(x.Entry.CreatedUtc, now, days)).ToArray()) { File.Delete(PathFor(item.Entry.Id)); items.Remove(item); }
        var sorted = items.OrderByDescending(x => x.Entry.CreatedUtc).ToList();
        long total = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            total += sorted[i].Bytes;
            if (i >= HistoryPolicy.MaxEntries || total > HistoryPolicy.MaxBytes) { File.Delete(PathFor(sorted[i].Entry.Id)); items.Remove(sorted[i]); }
        }
        return items;
    }
    public static Task<bool> RecordAsync(string raw, string result, byte[] wav, bool includeAudio, long expected)
    {
        return Task.Run(() =>
        {
            if (!Gate.Wait(0)) return false; // Never queue recordings in memory indefinitely.
            try
            {
                var prefs = Preferences.Load();
                if (!prefs.HistoryEnabled || (includeAudio && !prefs.HistoryAudioEnabled)) return false;
                var entry = new HistoryEntry { Id = Guid.NewGuid(), CreatedUtc = DateTimeOffset.UtcNow, Raw = raw, Result = result, Audio = includeAudio ? wav : null, HasAudio = includeAudio };
                if (!HistoryPolicy.Eligible(entry.Raw, entry.Result, entry.Audio)) return false;
                byte[] encrypted = Encrypt(entry);
                if (encrypted.Length > MaxFile) return false;
                Prune(prefs.HistoryDays);
                lock (CommitGate)
                {
                    if (epoch != expected) return false;
                    prefs = Preferences.Load();
                    if (!prefs.HistoryEnabled || (includeAudio && !prefs.HistoryAudioEnabled)) return false;
                    AtomicWrite(PathFor(entry.Id), encrypted);
                }
                Prune(prefs.HistoryDays);
                return true;
            }
            catch { return false; }
            finally { Gate.Release(); }
        });
    }
    public static Task CleanupAsync(int days) => Task.Run(() => { Gate.Wait(); try { Prune(days); } finally { Gate.Release(); } });
    public static Task<List<HistoryEntry>> ListAsync(int days) => Task.Run(() =>
    {
        Gate.Wait(); try { return Prune(days).OrderByDescending(x => x.Entry.CreatedUtc).Select(x => x.Entry).ToList(); } finally { Gate.Release(); }
    });
    public static Task<HistoryEntry> GetAsync(Guid id) => Task.Run(() =>
    {
        if (id == Guid.Empty) throw new ArgumentException();
        Gate.Wait(); try { return Read(PathFor(id)); } finally { Gate.Release(); }
    });
    public static Task DeleteAsync(Guid id) => Task.Run(() =>
    {
        if (id == Guid.Empty) throw new ArgumentException();
        Invalidate(); Gate.Wait(); try { File.Delete(PathFor(id)); File.Delete(PathFor(id) + ".tmp"); } finally { Invalidate(); Gate.Release(); }
    });
    public static Task ClearAsync() => Task.Run(() =>
    {
        Invalidate(); Gate.Wait();
        try { if (Directory.Exists(Dir)) { foreach (var f in Directory.GetFiles(Dir, "*.dat")) File.Delete(f); foreach (var f in Directory.GetFiles(Dir, "*.tmp")) File.Delete(f); } }
        finally { Invalidate(); Gate.Release(); }
    });
    public static Task ClearAudioAsync() => Task.Run(() =>
    {
        Invalidate(); Gate.Wait();
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var temp in Directory.GetFiles(Dir, "*.tmp")) File.Delete(temp);
            foreach (var path in Directory.GetFiles(Dir, "*.dat"))
            {
                var e = Read(path);
                if (e.Audio == null) continue;
                CryptographicOperations.ZeroMemory(e.Audio); e.Audio = null; e.HasAudio = false;
                AtomicWrite(path, Encrypt(e));
            }
        }
        finally { Invalidate(); Gate.Release(); }
    });
}
