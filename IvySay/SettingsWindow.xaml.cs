using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NAudio.Wave;

namespace IvySay;
public partial class SettingsWindow : Window
{
    private bool personalizationReadable = true;
    private bool initialized;
    public event EventHandler? PreferencesSaved;
    private int saveVersion;
    internal bool IsSecretEditor(TextBoxBase editor) => ReferenceEquals(editor, VisibleApiKey);
    private void MarkDirty(object sender, RoutedEventArgs e)
    {
        if (!initialized || e.OriginalSource is not (ComboBox or CheckBox or TextBox) ||
            e.OriginalSource == VisibleApiKey || e.OriginalSource == Vocabulary || e.OriginalSource == Corrections || e.OriginalSource == UsePersonalization) return;
        saveVersion++;
        GeneralSettingsSave.Content = "Save settings";
        var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#5B7BFF"), BlurRadius = 14, ShadowDepth = 0 };
        GeneralSettingsSave.Effect = glow;
        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(0.25, 0.7, TimeSpan.FromSeconds(1.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }
    private async void SavedFeedback()
    {
        int version = ++saveVersion;
        GeneralSettingsSave.Effect = null;
        GeneralSettingsSave.Content = "Saved";
        await Task.Delay(1200);
        if (saveVersion == version) GeneralSettingsSave.Content = "Save settings";
    }
    private void ToggleKeyVisibility(object sender, RoutedEventArgs e)
    {
        if (VisibleApiKey.Visibility == Visibility.Visible)
        {
            ApiKey.Password = VisibleApiKey.Text;
            VisibleApiKey.Clear();
            VisibleApiKey.Visibility = Visibility.Collapsed;
            ApiKey.Visibility = Visibility.Visible;
            KeyVisibility.Content = "Show";
            ApiKey.Focus();
        }
        else
        {
            VisibleApiKey.Text = ApiKey.Password;
            ApiKey.Clear();
            ApiKey.Visibility = Visibility.Collapsed;
            VisibleApiKey.Visibility = Visibility.Visible;
            KeyVisibility.Content = "Hide";
            VisibleApiKey.Focus();
        }
    }
    private void ClearKeyEditors()
    {
        ApiKey.Clear(); VisibleApiKey.Clear(); VisibleApiKey.Visibility = Visibility.Collapsed;
        ApiKey.Visibility = Visibility.Visible; KeyVisibility.Content = "Show";
    }
    private sealed record MicrophoneOption(string Label, string? DeviceName) { public override string ToString() => Label; }
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
        HidePillWhenIdle.IsChecked = preferences.HidePillWhenIdle;
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
        catch { personalizationReadable = false; IvyDialog.Show("Could not decrypt personalization data for this Windows account. Existing data was not changed. Delete it explicitly before creating a new profile.", "Ivy Say"); }
        Shortcut.SelectedIndex = preferences.Shortcut switch { "CtrlShiftSpace" => 1, "AltShiftSpace" => 2, _ => 0 };
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(MarkDirty));
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(MarkDirty));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(MarkDirty));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(MarkDirty));
        KeySection.Collapsed += (_, _) =>
        {
            if (VisibleApiKey.Visibility == Visibility.Visible)
            {
                ApiKey.Password = VisibleApiKey.Text; VisibleApiKey.Clear();
                VisibleApiKey.Visibility = Visibility.Collapsed; ApiKey.Visibility = Visibility.Visible; KeyVisibility.Content = "Show";
            }
        };
        Closed += (_, _) => ClearKeyEditors();
        initialized = true;
    }
    private void SavePreferences(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = Preferences.Load();
            if (!int.TryParse(ChunkThreshold.Text, out int threshold) || threshold < 14 || threshold > 120) { IvyDialog.Show("Chunk threshold must be between 14 and 120 seconds.", "Ivy Say"); return; }
            if (!int.TryParse(FormatterWordThreshold.Text, out int formatterThreshold) || formatterThreshold is < 20 or > 500) { IvyDialog.Show("Formatter word threshold must be between 20 and 500.", "Ivy Say"); return; }
            string security = SecurityLevel.SelectedIndex switch { 0 => "Unrestricted", 2 => "ReviewAll", _ => "Guarded" };
            if (security == "Unrestricted" && existing.SecurityLevel != "Unrestricted" &&
                IvyDialog.Show("No restrictions can type generated commands, control characters, or line breaks directly into terminals and other apps. This can execute commands or send messages. Continue?", "Ivy Say · Security", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (StreamingMode.SelectedIndex == 1 && security == "Unrestricted" && (!existing.StreamDirect || existing.SecurityLevel != "Unrestricted") &&
                IvyDialog.Show("Direct streaming types partial AI output immediately. It cannot be undone automatically; a failure or focus change may leave partial text, and terminal commands may run. Enable it?", "Ivy Say · Streaming", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            new Preferences { HidePillWhenIdle = HidePillWhenIdle.IsChecked == true, SmartFormatterRouting = SmartFormatterRouting.IsChecked == true, FormatterWordThreshold = formatterThreshold, CompressionKbps = Compression.SelectedIndex switch { 1 => 24, 2 => 32, 3 => 48, _ => 0 }, EnableChunking = EnableChunking.IsChecked == true, ChunkThresholdSeconds = threshold, StreamDirect = StreamingMode.SelectedIndex == 1, SecurityLevel = security, UsePersonalization = existing.UsePersonalization, Language = SpokenLanguage.SelectedIndex switch { 1 => "en", 2 => "hi", 3 => "pa", _ => "auto" }, SpellingHints = SpellingHints.Text.Trim(), TranscriptionModel = Model.SelectedIndex == 1 ? "whisper-large-v3-turbo" : "whisper-large-v3", Microphone = (Microphone.SelectedItem as MicrophoneOption)?.DeviceName, Polish = Polish.IsChecked == true, ShowComparison = ShowComparison.IsChecked == true, WarmConnection = WarmConnection.IsChecked == true, Shortcut = Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" } }.Save();
            SavedFeedback();
            PreferencesSaved?.Invoke(this, EventArgs.Empty);
            if (existing.Shortcut != (Shortcut.SelectedIndex switch { 1 => "CtrlShiftSpace", 2 => "AltShiftSpace", _ => "CtrlWinSpace" }))
                IvyDialog.Show("Settings saved. Restart Ivy Say to use your new shortcut.", "Ivy Say");
        }
        catch { IvyDialog.Show("Could not save preferences.", "Ivy Say"); }
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
        SecurityDescription.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SecurityLevel.SelectedIndex == 0 ? "#F59E7A" : "#9CA3B5"));
    }
    private void SettingsTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingsTabs == null || GeneralSettingsSave == null || e.Source != SettingsTabs) return;
        // Personal words have their own explicit save/delete actions.
        GeneralSettingsSave.Visibility = SettingsTabs.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        if (initialized && SettingsTabs.SelectedContent is FrameworkElement view)
        {
            view.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            var shift = new TranslateTransform();
            view.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(150)));
        }
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
        if (IvyDialog.Show("Clear your local dictation statistics?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { UsageStatistics.Default.Clear(); RefreshStats(); }
        catch { IvyDialog.Show("Could not clear local statistics.", "Ivy Say"); }
    }
    private void SavePersonalization(object sender, RoutedEventArgs e)
    {
        if (!personalizationReadable) { IvyDialog.Show("Existing personalization could not be read. Delete it explicitly before saving a new profile.", "Ivy Say"); return; }
        try
        {
            PersonalizationStore.Save(new PersonalizationData { Vocabulary = Vocabulary.Text.Trim(), Corrections = Corrections.Text.Trim() });
            var preferences = Preferences.Load();
            preferences.UsePersonalization = UsePersonalization.IsChecked == true;
            preferences.Save();
            IvyDialog.Show("Personalization saved. It applies to the next dictation when enabled.", "Ivy Say");
        }
        catch (ArgumentException ex) { IvyDialog.Show(ex.Message, "Ivy Say"); }
        catch { IvyDialog.Show("Could not securely save personalization.", "Ivy Say"); }
    }
    private void DeletePersonalization(object sender, RoutedEventArgs e)
    {
        if (IvyDialog.Show("Delete your saved vocabulary and corrections from this device?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            PersonalizationStore.Delete();
            var preferences = Preferences.Load(); preferences.UsePersonalization = false; preferences.Save();
            Vocabulary.Clear(); Corrections.Clear(); UsePersonalization.IsChecked = false; personalizationReadable = true;
        }
        catch { IvyDialog.Show("Could not remove personalization data or update settings.", "Ivy Say"); }
    }
    private void RefreshStatus() => KeyStatus.Text = KeyStore.Exists ? "A key is saved securely. Enter a new key to replace it." : "Enter your Groq API key to get started. It stays encrypted on this device.";
    private void SaveKey(object sender, RoutedEventArgs e)
    {
        string key = (VisibleApiKey.Visibility == Visibility.Visible ? VisibleApiKey.Text : ApiKey.Password).Trim();
        if (key.Length < 10 || key.Length > 512 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)) { IvyDialog.Show("Enter a valid Groq API key without spaces.", "Ivy Say"); return; }
        try { KeyStore.Save(key); ClearKeyEditors(); RefreshStatus(); KeySection.IsExpanded = false; IvyDialog.Show("Key saved securely.", "Ivy Say"); }
        catch { IvyDialog.Show("Could not securely save the key for this Windows account.", "Ivy Say"); }
    }
    private void RemoveKey(object sender, RoutedEventArgs e)
    {
        try { KeyStore.Delete(); ClearKeyEditors(); RefreshStatus(); KeySection.IsExpanded = true; }
        catch { IvyDialog.Show("Could not remove the saved key.", "Ivy Say"); }
    }
    private void Quit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
