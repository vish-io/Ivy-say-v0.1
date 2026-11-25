using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NAudio.Wave;

namespace IvySay;
public partial class SettingsWindow : Window
{
    private bool personalizationReadable = true;
    private bool initialized;
    private bool capturingShortcut;
    private ShortcutBinding selectedShortcut;
    internal Func<ShortcutBinding, string?>? ApplyShortcut { get; set; }
    internal Action<bool>? ShortcutCaptureChanged { get; set; }
    internal Func<bool>? CanCaptureShortcut { get; set; }
    private void CaptureShortcut(object sender, RoutedEventArgs e)
    {
        if (capturingShortcut) { EndCapture(); return; }
        if (CanCaptureShortcut?.Invoke() == false) { IvyDialog.Show("Finish the current dictation before changing the shortcut.", "Ivy Say"); return; }
        capturingShortcut = true;
        ShortcutCaptureChanged?.Invoke(true);
        ShortcutCapture.Content = "Press shortcut… (click to cancel)";
        ShortcutCapture.Focus();
    }
    private void EndCapture()
    {
        if (!capturingShortcut) return;
        capturingShortcut = false;
        ShortcutCaptureChanged?.Invoke(false);
        ShortcutCapture.Content = ShortcutLabel(selectedShortcut);
    }
    private void ShortcutCaptureLostFocus(object sender, KeyboardFocusChangedEventArgs e) => EndCapture();
    private void ShortcutKeyDown(object sender, KeyEventArgs e)
    {
        if (!capturingShortcut) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        var mods = Keyboard.Modifiers;
        uint mask = ((mods & ModifierKeys.Alt) != 0 ? 1u : 0) | ((mods & ModifierKeys.Control) != 0 ? 2u : 0) | ((mods & ModifierKeys.Shift) != 0 ? 4u : 0) | ((mods & ModifierKeys.Windows) != 0 ? 8u : 0);
        var chosen = new ShortcutBinding(vk, mask, ShortcutMode.SelectedIndex == 1);
        if (!chosen.Valid) return;
        selectedShortcut = chosen;
        EndCapture();
        MarkDirty(this, new RoutedEventArgs(Selector.SelectionChangedEvent, ShortcutMode));
    }
    internal static string ShortcutLabel(ShortcutBinding binding)
    {
        var parts = new List<string>();
        if ((binding.Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((binding.Modifiers & 8) != 0) parts.Add("Win");
        if ((binding.Modifiers & 1) != 0) parts.Add("Alt");
        if ((binding.Modifiers & 4) != 0) parts.Add("Shift");
        parts.Add(KeyInterop.KeyFromVirtualKey(binding.Key).ToString());
        return string.Join(" + ", parts);
    }
    public event EventHandler? PreferencesSaved;
    private int saveVersion;
    internal bool IsSecretEditor(TextBoxBase editor) => ReferenceEquals(editor, VisibleApiKey);
    private void MarkDirty(object sender, RoutedEventArgs e)
    {
        if (!initialized || e.OriginalSource is not (ComboBox or CheckBox or TextBox) ||
            e.OriginalSource == VisibleApiKey || e.OriginalSource == Vocabulary || e.OriginalSource == Corrections || e.OriginalSource == Snippets || e.OriginalSource == UseSnippets || e.OriginalSource == UsePersonalization || e.OriginalSource == LearnCorrections || e.OriginalSource == LearnedWords || e.OriginalSource == LearnAppStyles || e.OriginalSource == AppStyleList) return;
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
        StartOnLogin.IsChecked = preferences.StartOnLogin;
        RefreshStartupStatus(preferences.StartOnLogin);
        Polish.IsChecked = preferences.Polish;
        SmartFormatterRouting.IsChecked = preferences.SmartFormatterRouting;
        FormatterWordThreshold.Text = preferences.FormatterWordThreshold.ToString();
        SecurityLevel.SelectedIndex = preferences.SecurityLevel switch { "Unrestricted" => 0, "ReviewAll" => 2, _ => 1 };
        SecurityLevelChanged(SecurityLevel, null!);
        ShowComparison.IsChecked = preferences.ShowComparison;
        StreamingMode.SelectedIndex = preferences.StreamDirect ? 1 : 0;
        StatusSounds.IsChecked = preferences.StatusSounds;
        QuickNotesEnabled.IsChecked = preferences.QuickNotesEnabled;
        QuickNotesFolder.Text = preferences.QuickNotesFolder;
        UseAppContext.IsChecked = preferences.UseAppContext;
        UseNearbyText.IsChecked = preferences.UseNearbyText;
        ShareNearbyText.IsChecked = preferences.ShareNearbyText;
        HistoryEnabled.IsChecked = preferences.HistoryEnabled;
        HistoryAudioEnabled.IsChecked = preferences.HistoryAudioEnabled;
        HistoryDays.SelectedIndex = preferences.HistoryDays switch { 7 => 0, 90 => 2, _ => 1 };
        UsePersonalization.IsChecked = preferences.UsePersonalization;
        LearnCorrections.IsChecked = preferences.LearnCorrections;
        LearningStatus.Text = LearningFeedback.Message;
        LearnAppStyles.IsChecked = preferences.LearnAppStyles;
        UseSnippets.IsChecked = preferences.UseSnippets;
        try { var data = PersonalizationStore.Load(); Vocabulary.Text = data.Vocabulary; LearnedWords.Text = data.LearnedWords; Corrections.Text = data.Corrections; Snippets.Text = data.Snippets; RefreshAppStyles(data); }
        catch { personalizationReadable = false; IvyDialog.Show("Could not decrypt personalization data for this Windows account. Existing data was not changed. Delete it explicitly before creating a new profile.", "Ivy Say"); }
        selectedShortcut = ShortcutBinding.From(preferences);
        ShortcutMode.SelectedIndex = selectedShortcut.Hold ? 1 : 0;
        ShortcutCapture.Content = ShortcutLabel(selectedShortcut);
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
        Closed += (_, _) => { EndCapture(); ClearKeyEditors(); };
        initialized = true;
    }
    private void SavePreferences(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = Preferences.Load();
            if (HistoryAudioEnabled.IsChecked == true && HistoryEnabled.IsChecked != true) { IvyDialog.Show("Enable dictation history before saving audio.", "Ivy Say"); return; }
            if (HistoryEnabled.IsChecked == true && !existing.HistoryEnabled && IvyDialog.Show("Save future dictated transcripts and inserted text encrypted on this Windows account? Sensitive information may be included. You can delete history later.", "Ivy Say · History", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (HistoryAudioEnabled.IsChecked == true && !(existing.HistoryEnabled && existing.HistoryAudioEnabled) && IvyDialog.Show("Also retain future microphone recordings encrypted with history on this device? They can contain sensitive speech. You can delete saved audio separately.", "Ivy Say · Audio history", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (ShareNearbyText.IsChecked == true && UseNearbyText.IsChecked != true) { IvyDialog.Show("Enable nearby text on this device before sharing it.", "Ivy Say"); return; }
            if (ShareNearbyText.IsChecked == true && !(existing.ShareNearbyText && existing.UseNearbyText) && IvyDialog.Show("Ivy Say may send up to 256 characters around your cursor to Groq with each dictation. This could include private text. Enable sharing?", "Ivy Say · Nearby text", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (QuickNotesEnabled.IsChecked == true && (string.IsNullOrWhiteSpace(QuickNotesFolder.Text) || !System.IO.Directory.Exists(QuickNotesFolder.Text))) { IvyDialog.Show("Choose an existing folder for Quick Notes first.", "Ivy Say"); return; }
            if (QuickNotesEnabled.IsChecked == true && !existing.QuickNotesEnabled && IvyDialog.Show("Quick Notes saves your dictated text unencrypted in the selected folder. Anyone with access to that folder may read it. Enable?", "Ivy Say · Quick Notes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (!int.TryParse(ChunkThreshold.Text, out int threshold) || threshold < 14 || threshold > 120) { IvyDialog.Show("Chunk threshold must be between 14 and 120 seconds.", "Ivy Say"); return; }
            if (!int.TryParse(FormatterWordThreshold.Text, out int formatterThreshold) || formatterThreshold is < 20 or > 500) { IvyDialog.Show("Formatter word threshold must be between 20 and 500.", "Ivy Say"); return; }
            string security = SecurityLevel.SelectedIndex switch { 0 => "Unrestricted", 2 => "ReviewAll", _ => "Guarded" };
            if (security == "Unrestricted" && existing.SecurityLevel != "Unrestricted" &&
                IvyDialog.Show("No restrictions can type generated commands, control characters, or line breaks directly into terminals and other apps. This can execute commands or send messages. Continue?", "Ivy Say · Security", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (StreamingMode.SelectedIndex == 1 && security == "Unrestricted" && (!existing.StreamDirect || existing.SecurityLevel != "Unrestricted") &&
                IvyDialog.Show("Direct streaming types partial AI output immediately. It cannot be undone automatically; a failure or focus change may leave partial text, and terminal commands may run. Enable it?", "Ivy Say · Streaming", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            EndCapture();
            var proposed = selectedShortcut with { Hold = ShortcutMode.SelectedIndex == 1 };
            var oldShortcut = ShortcutBinding.From(existing);
            string? shortcutError = proposed != oldShortcut ? ApplyShortcut?.Invoke(proposed) : null;
            if (shortcutError != null) { IvyDialog.Show(shortcutError, "Ivy Say · Shortcut"); return; }
            bool startOnLogin = StartOnLogin.IsChecked == true;
            bool startupChanged = startOnLogin != existing.StartOnLogin;
            StartupRegistration.Previous? previousStartup = null;
            if (startupChanged)
            {
                try { previousStartup = StartupRegistration.Read(); StartupRegistration.Set(startOnLogin); }
                catch
                {
                    if (proposed != oldShortcut) ApplyShortcut?.Invoke(oldShortcut);
                    bool restored = true;
                    if (previousStartup != null) try { StartupRegistration.Restore(previousStartup); } catch { restored = false; }
                    IvyDialog.Show(restored ? "Could not update Windows startup. Use a published Ivy Say application in a permanent folder and check Windows permissions." : "Could not update or restore Windows startup. Check Windows startup settings.", "Ivy Say · Startup");
                    RefreshStartupStatus(existing.StartOnLogin);
                    return;
                }
            }
            DictationHistory.Invalidate();
            try { new Preferences { ShortcutKey = proposed.Key, ShortcutModifiers = proposed.Modifiers, ShortcutMode = proposed.Hold ? "Hold" : "Toggle", HidePillWhenIdle = HidePillWhenIdle.IsChecked == true, StartOnLogin = startOnLogin, SmartFormatterRouting = SmartFormatterRouting.IsChecked == true, FormatterWordThreshold = formatterThreshold, CompressionKbps = Compression.SelectedIndex switch { 1 => 24, 2 => 32, 3 => 48, _ => 0 }, EnableChunking = EnableChunking.IsChecked == true, ChunkThresholdSeconds = threshold, StreamDirect = StreamingMode.SelectedIndex == 1, SecurityLevel = security, UsePersonalization = existing.UsePersonalization, LearnCorrections = existing.LearnCorrections, LearnAppStyles = existing.LearnAppStyles, UseSnippets = existing.UseSnippets, FormattingMode = existing.FormattingMode, StatusSounds = StatusSounds.IsChecked == true, QuickNotesEnabled = QuickNotesEnabled.IsChecked == true, QuickNotesFolder = QuickNotesFolder.Text, Language = SpokenLanguage.SelectedIndex switch { 1 => "en", 2 => "hi", 3 => "pa", _ => "auto" }, SpellingHints = SpellingHints.Text.Trim(), TranscriptionModel = Model.SelectedIndex == 1 ? "whisper-large-v3-turbo" : "whisper-large-v3", Microphone = (Microphone.SelectedItem as MicrophoneOption)?.DeviceName, Polish = Polish.IsChecked == true, ShowComparison = ShowComparison.IsChecked == true, UseAppContext = UseAppContext.IsChecked == true, UseNearbyText = UseNearbyText.IsChecked == true, ShareNearbyText = ShareNearbyText.IsChecked == true && UseNearbyText.IsChecked == true, HistoryEnabled = HistoryEnabled.IsChecked == true, HistoryAudioEnabled = HistoryAudioEnabled.IsChecked == true && HistoryEnabled.IsChecked == true, HistoryDays = HistoryDays.SelectedIndex switch { 0 => 7, 2 => 90, _ => 30 }, Shortcut = existing.Shortcut }.Save(); }
            catch
            {
                if (proposed != oldShortcut) ApplyShortcut?.Invoke(oldShortcut);
                bool restored = true;
                if (previousStartup != null) try { StartupRegistration.Restore(previousStartup); } catch { restored = false; }
                IvyDialog.Show(restored ? "Could not save preferences." : "Could not save preferences or restore Windows startup. Check Windows startup settings.", "Ivy Say");
                RefreshStartupStatus(existing.StartOnLogin);
                return;
            }
            DictationHistory.Invalidate();
            _ = CleanupHistoryAsync();
            selectedShortcut = proposed;
            RefreshStartupStatus(startOnLogin);
            SavedFeedback();
            PreferencesSaved?.Invoke(this, EventArgs.Empty);

        }
        catch { IvyDialog.Show("Could not save preferences.", "Ivy Say"); }
    }
    private void RefreshStartupStatus(bool enabled)
    {
        if (!enabled) { StartupStatus.Text = "Off. Open Ivy Say manually to use the shortcut."; return; }
        try
        {
            var saved = StartupRegistration.Read();
            string? expected = StartupCommand.ForExecutable(Environment.ProcessPath);
            StartupStatus.Text = expected != null && saved.Existed && saved.Value is string value && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)
                ? "Configured for this Windows account. Windows may also control startup in its settings."
                : "Not configured yet. Open the published Ivy Say app from its permanent location to set it up.";
        }
        catch { StartupStatus.Text = "Could not check Windows startup settings."; }
    }
    private async Task CleanupHistoryAsync()
    {
        try { await DictationHistory.CleanupAsync(Preferences.Load().HistoryDays); }
        catch { /* Existing history is retained if cleanup fails. */ }
    }
    private void OpenHistory(object sender, RoutedEventArgs e) => new HistoryWindow { Owner = this }.Show();
    private void ChooseNotesFolder(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose where unencrypted Quick Notes are saved", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) QuickNotesFolder.Text = dialog.SelectedPath;
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
            var data = PersonalizationStore.Load();
            data.Vocabulary = Vocabulary.Text.Trim(); data.LearnedWords = LearnedWords.Text.Trim(); data.Corrections = Corrections.Text.Trim(); data.Snippets = Snippets.Text.Trim();
            PersonalizationStore.Save(data);
            var preferences = Preferences.Load();
            preferences.UsePersonalization = UsePersonalization.IsChecked == true;
            preferences.LearnCorrections = LearnCorrections.IsChecked == true;
            preferences.LearnAppStyles = LearnAppStyles.IsChecked == true;
            preferences.UseSnippets = UseSnippets.IsChecked == true;
            preferences.Save();
            IvyDialog.Show("Personalization saved. It applies to the next dictation when enabled.", "Ivy Say");
        }
        catch (ArgumentException ex) { IvyDialog.Show(ex.Message, "Ivy Say"); }
        catch { IvyDialog.Show("Could not securely save personalization.", "Ivy Say"); }
    }
    private void RefreshAppStyles(PersonalizationData data)
    {
        AppStyleList.Items.Clear();
        foreach (var profile in data.AppStyles.OrderBy(p => p.App, StringComparer.OrdinalIgnoreCase))
            AppStyleList.Items.Add(new ComboBoxItem { Content = AppStyleProfiles.Summary(profile), Tag = profile.App });
        if (AppStyleList.Items.Count > 0) AppStyleList.SelectedIndex = 0;
    }
    private void RemoveAppStyle(object sender, RoutedEventArgs e)
    {
        if (!personalizationReadable || AppStyleList.SelectedItem is not ComboBoxItem item || item.Tag is not string name) return;
        try { var data = PersonalizationStore.Load(); data.AppStyles.RemoveAll(p => p.App == name); PersonalizationStore.Save(data); RefreshAppStyles(data); }
        catch { IvyDialog.Show("Could not remove the app profile.", "Ivy Say"); }
    }
    private void ClearAppStyles(object sender, RoutedEventArgs e)
    {
        if (!personalizationReadable || IvyDialog.Show("Delete all learned app profiles?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { var data = PersonalizationStore.Load(); data.AppStyles.Clear(); PersonalizationStore.Save(data); RefreshAppStyles(data); }
        catch { IvyDialog.Show("Could not remove app profiles.", "Ivy Say"); }
    }
    private void DeletePersonalization(object sender, RoutedEventArgs e)
    {
        if (IvyDialog.Show("Delete your saved vocabulary, learned words, corrections, app profiles and snippets from this device?", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            PersonalizationStore.Delete();
            var preferences = Preferences.Load(); preferences.UsePersonalization = false; preferences.LearnCorrections = false; preferences.LearnAppStyles = false; preferences.UseSnippets = false; preferences.Save();
            Vocabulary.Clear(); LearnedWords.Clear(); Corrections.Clear(); Snippets.Clear(); UseSnippets.IsChecked = false; UsePersonalization.IsChecked = false; LearnCorrections.IsChecked = false; LearnAppStyles.IsChecked = false; AppStyleList.Items.Clear(); personalizationReadable = true;
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
