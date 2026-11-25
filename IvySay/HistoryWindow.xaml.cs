using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace IvySay;
public partial class HistoryWindow : Window
{
    private sealed record Row(HistoryEntry Entry, string Label);
    private WaveOutEvent? player;
    private WaveFileReader? reader;
    private byte[]? playingAudio;
    private bool closed;
    private int playbackVersion;
    public HistoryWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => { closed = true; StopPlayback(); Entries.ItemsSource = null; Raw.Clear(); Result.Clear(); };
    }
    private async Task RefreshAsync()
    {
        try
        {
            Status.Text = "Loading…";
            var list = await DictationHistory.ListAsync(Preferences.Load().HistoryDays);
            if (closed) return;
            Entries.ItemsSource = list.Select(e => new Row(e, e.CreatedUtc.ToLocalTime().ToString("g") + (e.HasAudio ? " · audio" : ""))).ToList();
            Status.Text = list.Count == 0 ? "No saved dictations." : $"{list.Count} saved · encrypted on this device.";
        }
        catch { if (!closed) Status.Text = "Could not read history. You can delete all history if needed."; }
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StopPlayback();
        var row = Entries.SelectedItem as Row;
        Raw.Text = row?.Entry.Raw ?? ""; Result.Text = row?.Entry.Result ?? "";
        Play.IsEnabled = row?.Entry.HasAudio == true; Delete.IsEnabled = row != null;
    }
    private async void PlayAudio(object sender, RoutedEventArgs e)
    {
        if (Entries.SelectedItem is not Row selected) return;
        StopPlayback(); Play.IsEnabled = false;
        int version = playbackVersion;
        try
        {
            var item = await DictationHistory.GetAsync(selected.Entry.Id);
            if (closed || version != playbackVersion || !ReferenceEquals(Entries.SelectedItem, selected) || item.Audio == null) { if (item.Audio != null) CryptographicOperations.ZeroMemory(item.Audio); return; }
            playingAudio = item.Audio;
            reader = new WaveFileReader(new MemoryStream(playingAudio, writable: false));
            player = new WaveOutEvent(); player.Init(reader); player.Play();
        }
        catch { StopPlayback(); if (!closed) IvyDialog.Show("Could not play the saved audio.", "Ivy Say"); }
        finally { if (!closed) Play.IsEnabled = (Entries.SelectedItem as Row)?.Entry.HasAudio == true; }
    }
    private void StopAudio(object sender, RoutedEventArgs e) => StopPlayback();
    private void StopPlayback() { playbackVersion++; player?.Stop(); player?.Dispose(); player = null; reader?.Dispose(); reader = null; if (playingAudio != null) CryptographicOperations.ZeroMemory(playingAudio); playingAudio = null; }
    private async void DeleteSelected(object sender, RoutedEventArgs e)
    {
        if (Entries.SelectedItem is not Row row || IvyDialog.Show("Delete this dictation and its saved audio?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        StopPlayback();
        try { await DictationHistory.DeleteAsync(row.Entry.Id); await RefreshAsync(); }
        catch { IvyDialog.Show("Could not delete this history entry.", "Ivy Say"); }
    }
    private async void DeleteAudio(object sender, RoutedEventArgs e)
    {
        if (IvyDialog.Show("Delete all saved history audio while keeping the text?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        StopPlayback();
        try { await DictationHistory.ClearAudioAsync(); await RefreshAsync(); }
        catch { IvyDialog.Show("Could not delete all audio. Some entries may have been updated; please check.", "Ivy Say"); await RefreshAsync(); }
    }
    private async void DeleteAll(object sender, RoutedEventArgs e)
    {
        if (IvyDialog.Show("Delete all saved dictations and audio?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        StopPlayback();
        try { await DictationHistory.ClearAsync(); await RefreshAsync(); }
        catch { IvyDialog.Show("Could not delete all history. Some entries may remain; please check.", "Ivy Say"); await RefreshAsync(); }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
