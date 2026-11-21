using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace IvySay;
public partial class MainWindow : Window
{
    private readonly GroqClient groq = new();
    private readonly CancellationTokenSource lifetime = new();
    private AudioRecorder? recorder;
    private ChunkedTranscription? chunkSession;
    private CancellationTokenSource? recordingCancellation;
    private Preferences? recordingPreferences;
    private PersonalizationData? recordingPersonal;
    private readonly DispatcherTimer chunkTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private IntPtr target;
    private IntPtr targetControl;
    private string shortcutLabel = "Ctrl + Win + Space";
    private bool busy;
    private bool hotkeyRegistered;
    private SettingsWindow? settings;
    private HwndSource? source;
    private readonly DispatcherTimer recordingTimer = new() { Interval = TimeSpan.FromSeconds(115) };
    public MainWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 32;
        Top = SystemParameters.WorkArea.Bottom - Height - 32;
        chunkTimer.Tick += (_, _) => { if (recorder != null && recordingPreferences?.EnableChunking == true) chunkSession?.Update(recorder.SnapshotPcm()); };
        recordingTimer.Tick += async (_, _) => { recordingTimer.Stop(); if (recorder != null) await ToggleAsync(); };
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { if (!KeyStore.Exists) ShowSettings(); };
        Closed += (_, _) => { recordingTimer.Stop(); chunkTimer.Stop(); lifetime.Cancel(); recordingCancellation?.Cancel(); recorder?.Dispose(); if (hotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, 1); source?.RemoveHook(Hook); groq.Dispose(); lifetime.Dispose(); };
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(Hook);
        var shortcut = Preferences.Load().Shortcut;
        uint modifiers = shortcut switch { "CtrlShiftSpace" => Native.MOD_CONTROL | Native.MOD_SHIFT, "AltShiftSpace" => Native.MOD_ALT | Native.MOD_SHIFT, _ => Native.MOD_CONTROL | Native.MOD_WIN };
        shortcutLabel = shortcut switch { "CtrlShiftSpace" => "Ctrl + Shift + Space", "AltShiftSpace" => "Alt + Shift + Space", _ => "Ctrl + Win + Space" };
        hotkeyRegistered = Native.RegisterHotKey(source.Handle, 1, modifiers | Native.MOD_NOREPEAT, Native.VK_SPACE);
        if (!hotkeyRegistered) SetStatus("Shortcut unavailable", "Change shortcut in settings, then restart");
        else SetStatus("Ready to listen", shortcutLabel);
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == 1) { handled = true; _ = ToggleAsync(); }
        return IntPtr.Zero;
    }
    private async Task ToggleAsync()
    {
        if (busy || settings?.IsVisible == true) return;
        if (recorder == null)
        {
            if (!KeyStore.Exists) { ShowSettings(); return; }
            try
            {
                target = Native.GetForegroundWindow();
                if (target == IntPtr.Zero || target == source?.Handle) throw new InvalidOperationException("Focus the app where you want to type first.");
                targetControl = Native.FocusedControl(target);
                var settingsSnapshot = Preferences.Load();
                string recordingKey = KeyStore.Load();
                var personal = settingsSnapshot.UsePersonalization ? PersonalizationStore.Load() : new PersonalizationData();
                string hints = settingsSnapshot.UsePersonalization ? PersonalizationRules.BuildPrompt(personal.Vocabulary, settingsSnapshot.SpellingHints) : settingsSnapshot.SpellingHints;
                recordingPreferences = settingsSnapshot;
                recordingPersonal = personal;
                recordingCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                chunkSession = new ChunkedTranscription(settingsSnapshot.ChunkThresholdSeconds, settingsSnapshot.CompressionKbps, hints,
                    (audio, opus, prompt, token) => groq.TranscribeAudioAsync(audio, opus, recordingKey, settingsSnapshot.TranscriptionModel, settingsSnapshot.Language, prompt, token), recordingCancellation.Token);
                recorder = new AudioRecorder(settingsSnapshot.Microphone);
                recorder.Start();
                recordingTimer.Start();
                chunkTimer.Start();
                if (settingsSnapshot.WarmConnection)
                {
                    CancellationToken token = lifetime.Token;
                    _ = Task.Run(async () =>
                    {
                        try { await groq.WarmConnectionAsync(KeyStore.Load(), token); }
                        catch { /* Warmup is best-effort and must never prevent recording. */ }
                    });
                }
                SetStatus("Listening…", "Press shortcut again to finish");
            }
            catch (Exception ex) { recordingCancellation?.Cancel(); recordingCancellation?.Dispose(); recordingCancellation = null; chunkSession = null; recorder?.Dispose(); recorder = null; ShowError(ex); }
            return;
        }
        recordingTimer.Stop();
        chunkTimer.Stop();
        busy = true;
        var current = recorder;
        recorder = null;
        string? recoverableText = null;
        string? formattingError = null;
        bool allowUnsafeCopy = false;
        try
        {
            SetStatus("Thinking…", "Transcribing your voice");
            byte[] wav = await current.StopAsync();
            // Audio is 16 kHz, mono, 16-bit PCM with a 44-byte WAV header.
            TimeSpan recordingDuration = TimeSpan.FromSeconds((wav.Length - 44) / 32000d);
            var preferences = recordingPreferences!;
            var personal = recordingPersonal!;
            string key = KeyStore.Load();
            allowUnsafeCopy = preferences.SecurityLevel == "Unrestricted";
            var transcriptionTimer = Stopwatch.StartNew();
            if (preferences.EnableChunking) chunkSession!.Update(wav[44..]);
            string raw = await chunkSession!.FinishAsync(wav);
            if (chunkSession.UsedFullRecordingFallback) SetStatus("Transcribed", "Full recording retry used");
            transcriptionTimer.Stop();
            TimeSpan? formattingTime = null;
            TimeSpan? firstDeltaTime = null;
            string? polishedText = null;
            if (raw.Length == 0) { SetStatus("Nothing heard", "Try speaking a little louder"); return; }
            string corrected = preferences.UsePersonalization ? PersonalizationRules.Parse(personal.Corrections).Apply(raw) : raw;
            recoverableText = corrected;
            bool structured = System.Text.RegularExpressions.Regex.IsMatch(raw, @"^\s*(code|bullet|bullets)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            bool directComplete = false;
            string formattingModel = FormatterRouter.Primary;
            bool usedFallback = false;
            FirstWordTimer? firstWordTimer = null;
            if (preferences.Polish)
            {
                SetStatus("Thinking…", "Polishing your words");
                bool direct = preferences.StreamDirect && allowUnsafeCopy;
                bool directAttempted = false;
                var streamed = new StringBuilder();
                try
                {
                    if (direct)
                    {
                        for (int i = 0; i < 100 && Native.ShortcutHeld(); i++) await Task.Delay(30, lifetime.Token);
                        if (Native.ShortcutHeld()) throw new InvalidOperationException("The shortcut keys were still held.");
                    }
                    var formattingTimer = Stopwatch.StartNew();
                    var currentWordTimer = new FirstWordTimer(() => formattingTimer.Elapsed);
                    firstWordTimer = currentWordTimer;
                    string polished = await FormatterRouter.RunAsync(
                        (model, callback, token) => groq.PolishStreamingAsync(corrected, key, model, callback, token),
                        fragment =>
                    {
                        firstDeltaTime ??= formattingTimer.Elapsed;
                        streamed.Append(fragment);
                        int start = Math.Max(0, streamed.Length - 90);
                        string preview = streamed.ToString(start, streamed.Length - start).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                        SetStatus(direct ? "Typing…" : "Polishing…", preview);
                        if (direct)
                        {
                            if (Native.ShortcutHeld()) throw new InvalidOperationException("A shortcut key was held during insertion.");
                            directAttempted = true; // Even a failed call can insert part of a fragment.
                            Native.TypeText(fragment, target, targetControl, currentWordTimer.Observe);
                        }
                        return Task.CompletedTask;
                    },
                    () =>
                    {
                        usedFallback = true;
                        formattingModel = FormatterRouter.Fallback;
                        streamed.Clear();
                        firstDeltaTime = null;
                        SetStatus("Switching model…", "Trying GPT-OSS 120B");
                    }, direct, lifetime.Token);
                    formattingTime = formattingTimer.Elapsed;
                    if (string.IsNullOrWhiteSpace(polished)) throw new InvalidOperationException("Formatting returned an empty result.");
                    if (direct && !structured && !char.IsWhiteSpace(polished[^1]))
                    {
                        if (Native.ShortcutHeld()) throw new InvalidOperationException("A shortcut key was held during insertion.");
                        Native.TypeText(" ", target, targetControl, currentWordTimer.Observe);
                    }
                    if (direct) currentWordTimer.Complete();
                    directComplete = direct;
                    polishedText = direct ? polished : polished.Trim();
                    recoverableText = polishedText;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    if (directAttempted)
                    {
                        SetStatus("Stream interrupted", "Check text already inserted");
                        new RecoveryWindow(streamed.ToString(), "Streaming stopped before completion. Some or all of this partial text may already be in the original app. Check it before copying; do not blindly paste it again.", allowUnsafeCopy).Show();
                        recoverableText = null;
                        return;
                    }
                    // Never display or log the provider response, transcript, or API key.
                    formattingError = ex switch
                    {
                        HttpRequestException { StatusCode: not null } httpError => $"Groq HTTP {(int)httpError.StatusCode.Value}",
                        HttpRequestException => "Network error",
                        OperationCanceledException => "Request timed out",
                        System.IO.IOException => "Connection interrupted",
                        JsonException or KeyNotFoundException => "Invalid Groq response",
                        InvalidOperationException => "Empty or incomplete result",
                        _ => "Formatting error"
                    };
                }
            }
            if (directComplete)
            {
                _ = UsageStatistics.Default.RecordAsync(polishedText!, recordingDuration, DateTime.Now);
                recoverableText = null;
                SetCompletionStatus(firstWordTimer, usedFallback);
                if (preferences.ShowComparison)
                    new ComparisonWindow(raw, corrected, polishedText, preferences.TranscriptionModel + (chunkSession!.UsedFullRecordingFallback ? " · full WAV retry" : chunkSession.ChunkCount > 0 ? " · chunked" : " · full recording"), transcriptionTimer.Elapsed, formattingTime, firstDeltaTime, formattingError, formattingModel, usedFallback, preferences.Polish, preferences.UsePersonalization).Show();
                return;
            }
            // In unrestricted mode, skip the extra process lookup and optional review checks.
            var (riskyTarget, targetUnknown) = allowUnsafeCopy ? (false, false) : Native.AssessTarget(target);
            string? reviewReason = InsertionGuard.ReviewReason(recoverableText, riskyTarget, targetUnknown || targetControl == IntPtr.Zero, preferences.SecurityLevel);
            if (reviewReason != null)
            {
                SetStatus("Review before inserting", "Nothing typed automatically");
                new RecoveryWindow(recoverableText, (usedFallback && polishedText != null ? "GPT-OSS 120B fallback was used. " : "") + reviewReason + " Copying and pasting into a terminal can execute commands; verify the text and destination first.").Show();
                recoverableText = null;
                return;
            }
            // Do not inject while modifiers from the shortcut are still held.
            for (int i = 0; i < 100 && Native.ShortcutHeld(); i++) await Task.Delay(30, lifetime.Token);
            if (Native.ShortcutHeld()) throw new InvalidOperationException("The shortcut keys were still held.");
            // Separate consecutive prose dictations without adding trailing whitespace to code or lists.
            Native.TypeText(recoverableText + (structured ? "" : " "), target, targetControl, polishedText != null && firstWordTimer != null ? firstWordTimer.Observe : null);
            if (polishedText != null) firstWordTimer?.Complete();
            _ = UsageStatistics.Default.RecordAsync(recoverableText, recordingDuration, DateTime.Now);
            recoverableText = null;
            if (formattingError != null) SetStatus(preferences.UsePersonalization ? "Personalized text inserted" : "Raw transcript inserted", usedFallback ? $"{formattingError} · 120B failed" : $"{formattingError} · formatting failed");
            else if (polishedText != null) SetCompletionStatus(firstWordTimer, usedFallback);
            else SetStatus(preferences.UsePersonalization ? "Personalized text" : "Raw mode", shortcutLabel);
            if (preferences.ShowComparison)
                new ComparisonWindow(raw, corrected, polishedText, preferences.TranscriptionModel + (chunkSession!.UsedFullRecordingFallback ? " · full WAV retry" : chunkSession.ChunkCount > 0 ? " · chunked" : " · full recording"), transcriptionTimer.Elapsed, formattingTime, firstDeltaTime, formattingError, formattingModel, usedFallback, preferences.Polish, preferences.UsePersonalization).Show();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (recoverableText != null)
            {
                SetStatus("Review dictation", "Text was not fully inserted");
                new RecoveryWindow(recoverableText, "Text could not be fully inserted into the original window. Some text may already have been inserted.", allowUnsafeCopy).Show();
            }
            else ShowError(ex);
        }
        finally { recordingCancellation?.Cancel(); recordingCancellation?.Dispose(); recordingCancellation = null; chunkSession = null; recordingPreferences = null; recordingPersonal = null; current.Dispose(); busy = false; }
    }

    private void SetCompletionStatus(FirstWordTimer? timer, bool usedFallback)
    {
        if (timer?.FirstWord is TimeSpan elapsed) SetStatus($"Done in {elapsed.TotalSeconds:F1}s", usedFallback ? "First word · 120B fallback" : "AI start → first word");
        else SetStatus("AI formatted", usedFallback ? "120B fallback used" : "No measurable first word");
    }
    private void SetStatus(string title, string detail) { StatusTitle.Text = title; StatusDetail.Text = detail; }
    private void ShowError(Exception ex)
    {
        SetStatus("Could not complete", "See the message for details");
        MessageBox.Show(ex is InvalidOperationException ? ex.Message : "Something went wrong. Check microphone permissions, your key, and network connection.", "Ivy Say", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();
    private void ShowSettings()
    {
        if (recorder != null || busy) { SetStatus("Finish dictation first", "Press shortcut to stop recording"); return; }
        if (settings?.IsVisible == true) { settings.Activate(); return; }
        settings = new SettingsWindow();
        settings.Show();
        settings.Activate();
    }
    private void DragPill(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
}
