using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace IvySay;
public partial class SettingsWindow : Window
{
    private bool personalizationReadable = true;
    private sealed record MicrophoneOption(string Label, string? DeviceName);
    public SettingsWindow()
    {
        InitializeComponent(); RefreshStatus();
        var preferences = Preferences.Load();
        SpokenLanguage.SelectedIndex = preferences.Language switch { "en" => 1, "hi" => 2, "pa" => 3, _ => 0 };
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
        SecurityLevel.SelectedIndex = preferences.SecurityLevel switch { "Unrestricted" => 0, "ReviewAll" => 2, _ => 1 };
        SecurityLevelChanged(SecurityLevel, null!);
        ShowComparison.IsChecked = preferences.ShowComparison;
        StreamingMode.SelectedIndex = preferences.StreamDirect ? 1 : 0;
        WarmConnection.IsChecked = preferences.WarmConnection;
        UsePersonalization.IsChecked = preferences.UsePersonalization;
        try { var data = PersonalizationStore.Load(); Vocabulary.Text = data.Vocabulary; Corrections.Text = data.Corrections; }
        catch { personalizationReadable = false; MessageBox.Show("Could not decrypt personalization data for this Windows account. Existing data was not changed. Delete it explicitly before creating a new profile.", "Ivy Say"); }
        Shortcut.SelectedIndex = preferences.Shortcut switch { "CtrlShiftSpace" => 1, "AltShiftSpace" => 2, _ => 0 };
    }
    private void SavePreferences(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = Preferences.Load();
            string security = SecurityLevel.SelectedIndex switch { 0 => "Unrestricted", 2 => "ReviewAll", _ => "Guarded" };
            if (security == "Unrestricted" && existing.SecurityLevel != "Unrestricted" &&
                MessageBox.Show("No restrictions can type generated commands, control characters, or line breaks directly into terminals and other apps. This can execute commands or send messages. Continue?", "Ivy Say · Security", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (StreamingMode.SelectedIndex == 1 && security == "Unrestricted" && (!existing.StreamDirect || existing.SecurityLevel != "Unrestricted") &&
                MessageBox.Show("Direct streaming types partial AI output immediately. It cannot be undone automatically; a failure or focus change may leave partial text, and terminal commands may run. Enable it?", "Ivy Say · Streaming", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            new Preferences { StreamDirect = StreamingMode.SelectedIndex == 1, SecurityLevel = security, UsePersonalization = existing.UsePersonalization, Language = SpokenLanguage.SelectedIndex switch { 1 => "en", 2 => "hi", 3 => "pa", _ => "auto" }, SpellingHints = SpellingHints.Text.Trim(), TranscriptionModel = Model.SelectedIndex == 1 ? "whisper-large-v3-turbo" : "whisper-large-v3", Microphone = (Microphone.SelectedItem as MicrophoneOption)?.DeviceName, Polish = Polish.IsChecked == true, ShowComparison = ShowComparison.IsChecked == true, WarmConnection = WarmConnection.IsChecked == true, Shortcut = Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" } }.Save();
            MessageBox.Show("Preferences saved. Restart Ivy Say to apply shortcut changes.", "Ivy Say");
        }
        catch { MessageBox.Show("Could not save preferences.", "Ivy Say"); }
    }
    private void SecurityLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecurityDescription == null) return;
        SecurityDescription.Text = SecurityLevel.SelectedIndex switch
        {
            0 => "Automatic insertion without content or terminal review. Commands or line breaks may execute actions. Foreground and focus checks still apply.",
            2 => "Every result opens for review and manual copying. Unsafe control characters cannot be copied by Ivy Say.",
            _ => "Review known terminals, unverified targets, multiline, tabs and unsafe characters. Detection is best-effort and can miss sensitive apps."
        };
        SecurityDescription.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SecurityLevel.SelectedIndex == 0 ? "#a04b35" : "#687a70"));
    }
    private void CloseSettings(object sender, RoutedEventArgs e) => Close();
    private void SavePersonalization(object sender, RoutedEventArgs e)
    {
        if (!personalizationReadable) { MessageBox.Show("Existing personalization could not be read. Delete it explicitly before saving a new profile.", "Ivy Say"); return; }
        try
        {
            PersonalizationStore.Save(new PersonalizationData { Vocabulary = Vocabulary.Text.Trim(), Corrections = Corrections.Text.Trim() });
            var preferences = Preferences.Load();
            preferences.UsePersonalization = UsePersonalization.IsChecked == true;
            preferences.Save();
            MessageBox.Show("Personalization saved. It applies to the next dictation when enabled.", "Ivy Say");
        }
        catch (ArgumentException ex) { MessageBox.Show(ex.Message, "Ivy Say"); }
        catch { MessageBox.Show("Could not securely save personalization.", "Ivy Say"); }
    }
    private void DeletePersonalization(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Delete your saved vocabulary and corrections from this device?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            PersonalizationStore.Delete();
            var preferences = Preferences.Load(); preferences.UsePersonalization = false; preferences.Save();
            Vocabulary.Clear(); Corrections.Clear(); UsePersonalization.IsChecked = false; personalizationReadable = true;
        }
        catch { MessageBox.Show("Could not remove personalization data or update settings.", "Ivy Say"); }
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
