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
        InitializeComponent(); RefreshStatus(); RefreshStats(); KeySection.IsExpanded = !KeyStore.Exists;
        var preferences = Preferences.Load();
        SpokenLanguage.SelectedIndex = preferences.Language switch { "en" => 1, "hi" => 2, "pa" => 3, _ => 0 };
        SpellingHints.Text = preferences.SpellingHints;
        Compression.SelectedIndex = preferences.CompressionKbps switch { 24 => 1, 32 => 2, 48 => 3, _ => 0 };
        EnableChunking.IsChecked = preferences.EnableChunking;
        ChunkThreshold.Text = preferences.ChunkThresholdSeconds.ToString();
        Model.SelectedIndex = preferences.TranscriptionModel == "whisper-large-v3-turbo" ? 1 : 0;
        Microphone.Items.Add(new MicrophoneOption("Use Windows default", null));
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
        SmartFormatterRouting.IsChecked = preferences.SmartFormatterRouting;
        FormatterWordThreshold.Text = preferences.FormatterWordThreshold.ToString();
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
            if (!int.TryParse(ChunkThreshold.Text, out int threshold) || threshold < 14 || threshold > 120) { MessageBox.Show("Chunk threshold must be between 14 and 120 seconds.", "Ivy Say"); return; }
            if (!int.TryParse(FormatterWordThreshold.Text, out int formatterThreshold) || formatterThreshold is < 20 or > 500) { MessageBox.Show("Formatter word threshold must be between 20 and 500.", "Ivy Say"); return; }
            string security = SecurityLevel.SelectedIndex switch { 0 => "Unrestricted", 2 => "ReviewAll", _ => "Guarded" };
            if (security == "Unrestricted" && existing.SecurityLevel != "Unrestricted" &&
                MessageBox.Show("No restrictions can type generated commands, control characters, or line breaks directly into terminals and other apps. This can execute commands or send messages. Continue?", "Ivy Say · Security", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (StreamingMode.SelectedIndex == 1 && security == "Unrestricted" && (!existing.StreamDirect || existing.SecurityLevel != "Unrestricted") &&
                MessageBox.Show("Direct streaming types partial AI output immediately. It cannot be undone automatically; a failure or focus change may leave partial text, and terminal commands may run. Enable it?", "Ivy Say · Streaming", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            new Preferences { SmartFormatterRouting = SmartFormatterRouting.IsChecked == true, FormatterWordThreshold = formatterThreshold, CompressionKbps = Compression.SelectedIndex switch { 1 => 24, 2 => 32, 3 => 48, _ => 0 }, EnableChunking = EnableChunking.IsChecked == true, ChunkThresholdSeconds = threshold, StreamDirect = StreamingMode.SelectedIndex == 1, SecurityLevel = security, UsePersonalization = existing.UsePersonalization, Language = SpokenLanguage.SelectedIndex switch { 1 => "en", 2 => "hi", 3 => "pa", _ => "auto" }, SpellingHints = SpellingHints.Text.Trim(), TranscriptionModel = Model.SelectedIndex == 1 ? "whisper-large-v3-turbo" : "whisper-large-v3", Microphone = (Microphone.SelectedItem as MicrophoneOption)?.DeviceName, Polish = Polish.IsChecked == true, ShowComparison = ShowComparison.IsChecked == true, WarmConnection = WarmConnection.IsChecked == true, Shortcut = Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" } }.Save();
            MessageBox.Show(existing.Shortcut != (Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" })
                ? "Settings saved. Restart Ivy Say to use your new shortcut."
                : "Settings saved.", "Ivy Say");
        }
        catch { MessageBox.Show("Could not save preferences.", "Ivy Say"); }
    }
    private void SecurityLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecurityDescription == null) return;
        SecurityDescription.Text = SecurityLevel.SelectedIndex switch
        {
            0 => "Text is typed without safety review. Commands and line breaks may trigger actions.",
            2 => "Review and copy each result yourself. Unsafe characters are blocked from copying.",
            _ => "Some text and destinations are held for review. These checks can miss risks."
        };
        SecurityDescription.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SecurityLevel.SelectedIndex == 0 ? "#a04b35" : "#687a70"));
    }
    private void SettingsTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingsTabs == null || GeneralSettingsSave == null || e.Source != SettingsTabs) return;
        // Personal words have their own explicit save/delete actions.
        GeneralSettingsSave.Visibility = SettingsTabs.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void CloseSettings(object sender, RoutedEventArgs e) => Close();
    private void RefreshStats()
    {
        try
        {
            double? average = UsageStatistics.Default.SevenDayAverage(DateTime.Now);
            StatsValue.Text = average.HasValue ? $"{average.Value:F0} WPM" : "— WPM";
            StatsDetail.Text = average.HasValue ? "Average words per minute" : "Your average will appear after you start dictating.";
        }
        catch { StatsValue.Text = "— WPM"; StatsDetail.Text = "Statistics unavailable on this device."; }
    }
    private void ClearStats(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear your local dictation statistics?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { UsageStatistics.Default.Clear(); RefreshStats(); }
        catch { MessageBox.Show("Could not clear local statistics.", "Ivy Say"); }
    }
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
        try { KeyStore.Save(key); ApiKey.Clear(); RefreshStatus(); KeySection.IsExpanded = false; MessageBox.Show("Key saved securely.", "Ivy Say"); }
        catch { MessageBox.Show("Could not securely save the key for this Windows account.", "Ivy Say"); }
    }
    private void RemoveKey(object sender, RoutedEventArgs e)
    {
        try { KeyStore.Delete(); ApiKey.Clear(); RefreshStatus(); KeySection.IsExpanded = true; }
        catch { MessageBox.Show("Could not remove the saved key.", "Ivy Say"); }
    }
    private void Quit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
