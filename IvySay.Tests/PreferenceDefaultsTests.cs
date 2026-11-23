using System.Text.Json;
using IvySay;
using Xunit;
public class PreferenceDefaultsTests
{
    [Fact] public void NewInstallationHasRequestedDefaults()
    {
        var p = new Preferences(); var shortcut = ShortcutBinding.From(p);
        Assert.Equal("whisper-large-v3-turbo", p.TranscriptionModel); Assert.Equal("en", p.Language);
        Assert.Equal(24, p.CompressionKbps); Assert.True(p.EnableChunking); Assert.Equal(14, p.ChunkThresholdSeconds);
        Assert.True(p.SmartFormatterRouting); Assert.Equal(40, p.FormatterWordThreshold); Assert.True(p.Polish);
        Assert.Equal(0xC0, shortcut.Key); Assert.Equal(0u, shortcut.Modifiers); Assert.False(shortcut.Hold);
        Assert.Equal("Unrestricted", p.SecurityLevel); Assert.True(p.UseAppContext); Assert.True(p.StatusSounds);
        Assert.False(p.StreamDirect);
        Assert.DoesNotContain("WarmConnection", JsonSerializer.Serialize(p));
    }
    [Fact] public void ExistingSavedChoicesArePreserved()
    {
        var p = Preferences.ParseExisting("{\"TranscriptionModel\":\"whisper-large-v3\",\"Language\":\"pa\",\"Shortcut\":\"CtrlShiftSpace\",\"SecurityLevel\":\"Guarded\",\"CompressionKbps\":0,\"EnableChunking\":false,\"FormatterWordThreshold\":120,\"UseAppContext\":false,\"StatusSounds\":false,\"Polish\":false,\"WarmConnection\":false}");
        Assert.Equal("whisper-large-v3", p.TranscriptionModel); Assert.Equal("pa", p.Language);
        Assert.Equal(0, p.CompressionKbps); Assert.False(p.EnableChunking); Assert.Equal(120, p.FormatterWordThreshold);
        Assert.Equal("Guarded", p.SecurityLevel); Assert.False(p.UseAppContext); Assert.False(p.StatusSounds); Assert.False(p.Polish);
        Assert.Equal(new ShortcutBinding(0x20, 6, false), ShortcutBinding.From(p));
    }
    [Fact] public void OlderMissingValuesKeepOldDefaults()
    {
        var p = Preferences.ParseExisting("{\"AlwaysReview\":true}");
        Assert.Equal("ReviewAll", p.SecurityLevel); Assert.Equal("auto", p.Language); Assert.Equal(0, p.CompressionKbps);
        Assert.Equal("whisper-large-v3", p.TranscriptionModel); Assert.Equal(120, p.FormatterWordThreshold);
        Assert.False(p.UseAppContext); Assert.False(p.StatusSounds);
        Assert.Equal(new ShortcutBinding(0x20, 10, false), ShortcutBinding.From(p));
    }
}
