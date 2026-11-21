using System.IO;
using System.Text.Json;

namespace IvySay;

// Non-sensitive preferences only. Keys never belong in this file.
internal sealed class Preferences
{
    public string TranscriptionModel { get; set; } = "whisper-large-v3";
    public string Language { get; set; } = "auto";
    public string SpellingHints { get; set; } = "";
    public string Shortcut { get; set; } = "CtrlWinSpace";
    public string? Microphone { get; set; }
    // Legacy field retained for migrating existing settings.
    public bool AlwaysReview { get; set; }
    public string? SecurityLevel { get; set; }
    public bool UsePersonalization { get; set; }
    public bool WarmConnection { get; set; }
    public bool StreamDirect { get; set; }
    public bool ShowComparison { get; set; }
    public int CompressionKbps { get; set; }
    public int ChunkThresholdSeconds { get; set; } = 14;
    public bool EnableChunking { get; set; } = true;
    public bool Polish { get; set; } = true;
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "preferences.json");
    public static Preferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new Preferences();
            if (value.TranscriptionModel is not ("whisper-large-v3" or "whisper-large-v3-turbo")) value.TranscriptionModel = "whisper-large-v3";
            if (value.Language is not ("auto" or "en" or "hi" or "pa")) value.Language = "auto";
            if (value.SpellingHints is null || value.SpellingHints.Length > 160) value.SpellingHints = "";
            if (value.CompressionKbps is not (0 or 24 or 32 or 48)) value.CompressionKbps = 0;
            if (value.ChunkThresholdSeconds < 14 || value.ChunkThresholdSeconds > 120) value.ChunkThresholdSeconds = 14;
            value.SecurityLevel = NormalizeSecurityLevel(value.SecurityLevel, value.AlwaysReview);
            value.AlwaysReview = false;
            if (value.Shortcut is not ("CtrlWinSpace" or "CtrlShiftSpace" or "AltShiftSpace")) value.Shortcut = "CtrlWinSpace";
            return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new Preferences { SecurityLevel = "Guarded" }; }
    }
    public static string NormalizeSecurityLevel(string? level, bool legacyAlwaysReview) => level switch
    {
        "Unrestricted" or "Guarded" or "ReviewAll" => level,
        null => legacyAlwaysReview ? "ReviewAll" : "Guarded",
        _ => "Guarded"
    };
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, FilePath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
