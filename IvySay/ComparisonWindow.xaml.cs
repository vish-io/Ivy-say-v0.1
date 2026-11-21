using System.Windows;
using System.IO;
using NAudio.Wave;
namespace IvySay;
public partial class ComparisonWindow : Window
{
    private byte[]? capturedWav;
    private WaveOutEvent? player;
    private WaveFileReader? reader;
    public ComparisonWindow(string raw, string corrected, string? polished, string model, TimeSpan transcriptionTime, TimeSpan? formattingTime, TimeSpan? firstDeltaTime, string? error, string formattingModel, bool usedFallback, bool polishEnabled, bool personalized, byte[] wav, string microphone, string routingReason)
    {
        InitializeComponent();
        capturedWav = wav;
        Closed += (_, _) => { StopPlayback(); capturedWav = null; };
        RawText.Text = raw;
        PersonalText.Text = corrected;
        PersonalLabel.Text = personalized ? "2 · After your local corrections" : "2 · Local corrections disabled";
        if (polished != null)
        {
            OutputLabel.Text = "3 · AI formatter output (inserted)";
            OutputText.Text = polished;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · {formattingModel}{(usedFallback ? " (fallback; times include first attempt)" : " (selected)")}: first text: {firstDeltaTime!.Value.TotalSeconds:F2}s · AI total: {formattingTime!.Value.TotalSeconds:F2}s · routing: {routingReason}. The model may return text unchanged when no edit is needed.";
        }
        else
        {
            OutputLabel.Text = "3 · No AI output (stage 2 inserted)";
            OutputText.Text = corrected;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · " + (polishEnabled ? $"{(usedFallback ? $"{formattingModel} fallback failed" : "Formatting failed")}: {error ?? "unknown error"}." : "AI formatting is disabled in settings.");
        }
        Summary.Text += $"\nInput: {microphone} · {AudioSignal.Measure(wav.AsSpan(44)).Description}. Playback is the original WAV, before any optional compression.";
    }
    private void PlayAudio(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        try
        {
            reader = new WaveFileReader(new MemoryStream(capturedWav!, writable: false));
            player = new WaveOutEvent();
            player.Init(reader);
            player.Play();
        }
        catch { StopPlayback(); MessageBox.Show("Could not play the captured audio. Check your Windows output device.", "Ivy Say"); }
    }
    private void StopAudio(object sender, RoutedEventArgs e) => StopPlayback();
    private void StopPlayback() { player?.Stop(); player?.Dispose(); player = null; reader?.Dispose(); reader = null; }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
