using System.IO;
using System.Text.Json;

namespace IvySay;

// Non-sensitive preferences only. Keys never belong in this file.
internal sealed class Preferences
{
    public string TranscriptionModel { get; set; } = "whisper-large-v3-turbo";
    public string Language { get; set; } = "en";
    public string SpellingHints { get; set; } = "";
    public string Shortcut { get; set; } = "Oem3";
    public int ShortcutKey { get; set; }
    public uint ShortcutModifiers { get; set; }
    public string ShortcutMode { get; set; } = "Toggle";
    public string? Microphone { get; set; }
    // Legacy field retained for migrating existing settings.
    public bool AlwaysReview { get; set; }
    public string? SecurityLevel { get; set; } = "Unrestricted";
    public bool UsePersonalization { get; set; }
    public bool UseSnippets { get; set; }
    public string FormattingMode { get; set; } = "clean";
    public bool QuickNotesEnabled { get; set; }
    public string QuickNotesFolder { get; set; } = "";
    public bool StatusSounds { get; set; } = true;
    public bool UseAppContext { get; set; } = true;
    public bool StreamDirect { get; set; }
    public bool ShowComparison { get; set; }
    public bool HidePillWhenIdle { get; set; }
    public int CompressionKbps { get; set; } = 24;
    public int ChunkThresholdSeconds { get; set; } = 14;
    public bool EnableChunking { get; set; } = true;
    public bool SmartFormatterRouting { get; set; } = true;
    public int FormatterWordThreshold { get; set; } = 40;
    public bool Polish { get; set; } = true;
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "preferences.json");
    public static Preferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Preferences();
            return ParseExisting(File.ReadAllText(FilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new Preferences { SecurityLevel = "Guarded" }; }
    }
    internal static Preferences ParseExisting(string json)
    {
        using var document = JsonDocument.Parse(json);
        var value = JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Preferences must be a JSON object.");
        // Missing properties in older preference files retain the earlier defaults.
        if (!root.TryGetProperty(nameof(TranscriptionModel), out _)) value.TranscriptionModel = "whisper-large-v3";
        if (!root.TryGetProperty(nameof(Language), out _)) value.Language = "auto";
        if (!root.TryGetProperty(nameof(Shortcut), out _)) value.Shortcut = "CtrlWinSpace";
        if (!root.TryGetProperty(nameof(SecurityLevel), out _)) value.SecurityLevel = null;
        if (!root.TryGetProperty(nameof(CompressionKbps), out _)) value.CompressionKbps = 0;
        if (!root.TryGetProperty(nameof(FormatterWordThreshold), out _)) value.FormatterWordThreshold = 120;
        if (!root.TryGetProperty(nameof(StatusSounds), out _)) value.StatusSounds = false;
        if (!root.TryGetProperty(nameof(UseAppContext), out _)) value.UseAppContext = false;
        if (value.TranscriptionModel is not ("whisper-large-v3" or "whisper-large-v3-turbo")) value.TranscriptionModel = "whisper-large-v3";
        value.FormattingMode = FormattingModes.Normalize(value.FormattingMode);
        value.QuickNotesFolder ??= "";
        if (value.Language is not ("auto" or "en" or "hi" or "pa")) value.Language = "auto";
        if (value.SpellingHints is null || value.SpellingHints.Length > 160) value.SpellingHints = "";
        if (value.CompressionKbps is not (0 or 24 or 32 or 48)) value.CompressionKbps = 0;
        if (value.ChunkThresholdSeconds < 14 || value.ChunkThresholdSeconds > 120) value.ChunkThresholdSeconds = 14;
        if (value.FormatterWordThreshold is < 20 or > 500) value.FormatterWordThreshold = 120;
        value.SecurityLevel = NormalizeSecurityLevel(value.SecurityLevel, value.AlwaysReview);
        value.AlwaysReview = false;
        if (value.Shortcut is not ("Oem3" or "CtrlWinSpace" or "CtrlShiftSpace" or "AltShiftSpace")) value.Shortcut = "CtrlWinSpace";
        if (value.ShortcutMode is not ("Toggle" or "Hold")) value.ShortcutMode = "Toggle";
        if (value.ShortcutKey != 0 && !ShortcutBinding.From(value).Valid) { value.ShortcutKey = 0; value.ShortcutModifiers = 0; value.ShortcutMode = "Toggle"; }
        return value;
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
