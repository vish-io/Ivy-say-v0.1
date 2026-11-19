using System.IO;
using System.Text.Json;

namespace IvySay;

// Non-sensitive preferences only. Keys never belong in this file.
internal sealed class Preferences
{
    public string TranscriptionModel { get; set; } = "whisper-large-v3";
    public string Shortcut { get; set; } = "CtrlWinSpace";
    public string? Microphone { get; set; }
    public bool WarmConnection { get; set; }
    public bool ShowComparison { get; set; }
    public bool Polish { get; set; } = true;
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "preferences.json");
    public static Preferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new Preferences();
            if (value.TranscriptionModel is not ("whisper-large-v3" or "whisper-large-v3-turbo")) value.TranscriptionModel = "whisper-large-v3";
            if (value.Shortcut is not ("CtrlWinSpace" or "CtrlShiftSpace" or "AltShiftSpace")) value.Shortcut = "CtrlWinSpace";
            return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new Preferences(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, FilePath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
