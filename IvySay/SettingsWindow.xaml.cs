using System.Windows;
using NAudio.Wave;

namespace IvySay;
public partial class SettingsWindow : Window
{
    private sealed record MicrophoneOption(string Label, string? DeviceName);
    public SettingsWindow()
    {
        InitializeComponent(); RefreshStatus();
        var preferences = Preferences.Load();
        Language.SelectedIndex = preferences.Language switch { "en" => 1, "hi" => 2, "pa" => 3, _ => 0 };
        SpellingHints.Text = preferences.SpellingHints;
        Model.SelectedIndex = preferences.TranscriptionModel == "whisper-large-v3-turbo" ? 1 : 0;
        Microphone.Items.Add(new MicrophoneOption("Windows default", null));
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            string name = WaveIn.GetCapabilities(i).ProductName;
            Microphone.Items.Add(new MicrophoneOption(name, name));
        }
        Microphone.SelectedIndex = 0;
        if (!string.IsNullOrEmpty(preferences.Microphone))
        {
            var selected = Microphone.Items.Cast<MicrophoneOption>().FirstOrDefault(item => item.DeviceName == preferences.Microphone);
            if (selected == null)
            {
                selected = new MicrophoneOption(preferences.Microphone + " (unavailable)", preferences.Microphone);
                Microphone.Items.Add(selected);
            }
            Microphone.SelectedItem = selected;
        }
        Polish.IsChecked = preferences.Polish;
        ShowComparison.IsChecked = preferences.ShowComparison;
        WarmConnection.IsChecked = preferences.WarmConnection;
        Shortcut.SelectedIndex = preferences.Shortcut switch { "CtrlShiftSpace" => 1, "AltShiftSpace" => 2, _ => 0 };
    }
    private void SavePreferences(object sender, RoutedEventArgs e)
    {
        try
        {
            new Preferences { Language = Language.SelectedIndex switch { 1 => "en", 2 => "hi", 3 => "pa", _ => "auto" }, SpellingHints = SpellingHints.Text.Trim(), TranscriptionModel = Model.SelectedIndex == 1 ? "whisper-large-v3-turbo" : "whisper-large-v3", Microphone = (Microphone.SelectedItem as MicrophoneOption)?.DeviceName, Polish = Polish.IsChecked == true, ShowComparison = ShowComparison.IsChecked == true, WarmConnection = WarmConnection.IsChecked == true, Shortcut = Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" } }.Save();
            MessageBox.Show("Preferences saved. Restart Ivy Say to apply shortcut changes.", "Ivy Say");
        }
        catch { MessageBox.Show("Could not save preferences.", "Ivy Say"); }
    }
    private void RefreshStatus() => KeyStatus.Text = KeyStore.Exists ? "A key is saved securely. Enter a new key to replace it." : "Enter your Groq API key to get started. It stays encrypted on this device.";
    private void SaveKey(object sender, RoutedEventArgs e)
    {
        string key = ApiKey.Password.Trim();
        if (key.Length < 10 || key.Length > 512 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)) { MessageBox.Show("Enter a valid Groq API key without spaces.", "Ivy Say"); return; }
        try { KeyStore.Save(key); ApiKey.Clear(); RefreshStatus(); MessageBox.Show("Key saved securely.", "Ivy Say"); }
        catch { MessageBox.Show("Could not securely save the key for this Windows account.", "Ivy Say"); }
    }
    private void RemoveKey(object sender, RoutedEventArgs e)
    {
        try { KeyStore.Delete(); ApiKey.Clear(); RefreshStatus(); }
        catch { MessageBox.Show("Could not remove the saved key.", "Ivy Say"); }
    }
    private void Quit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
