using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
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
    private string? recordingKey;
    private Task<string?>? recordingAppName;
    private Task<NearbyContextReader.Snapshot?>? recordingNearby;
    private Task<HistoryFieldSafety.Target?>? recordingHistoryTarget;
    private long recordingHistoryGeneration;
    private IInputElement? settingsTargetElement;
    private readonly DispatcherTimer chunkTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private IntPtr target;
    private IntPtr targetControl;
    private string shortcutLabel = "Ctrl + Win + Space";
    private bool busy;
    private bool hotkeyRegistered;
    private bool startupRegistrationFailed;
    private bool selectionHotkeyRegistered;
    private bool quickNoteHotkeyRegistered, modeHotkeyRegistered, recordingQuickNote;
    private const int quickNoteHotkeyId = 9193, modeHotkeyId = 9194;
    private const int selectionHotkeyId = 9192;
    private int selectionLookupActive;
    private int learningGeneration;
    private int hotkeyId = 1;
    private ShortcutBinding currentShortcut;
    private HoldShortcut? holdShortcut;
    private bool holdSession, holdReleased;
    private bool shortcutCapture;
    private SettingsWindow? settings;
    private NotificationArea? notificationArea;
    private (string Text, string Explanation, bool AllowUnsafeCopy)? pendingRecovery;
    private HwndSource? source;
    private readonly DispatcherTimer idleHideTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer historyCleanupTimer = new() { Interval = TimeSpan.FromHours(1) };
    private readonly DispatcherTimer recordingTimer = new() { Interval = TimeSpan.FromSeconds(115) };
    public MainWindow(bool startupLaunch = false)
    {
        InitializeComponent();
        try { notificationArea = new NotificationArea(ShowSettings, () => Application.Current.Shutdown(), RecoverLast); }
        catch { notificationArea = null; } // Do not hide the only UI if the tray icon cannot be created.
        Left = SystemParameters.WorkArea.Right - Width - 32;
        Top = SystemParameters.WorkArea.Bottom - Height - 32;
        chunkTimer.Tick += (_, _) => { if (recorder != null && recordingPreferences?.EnableChunking == true) chunkSession?.Update(recorder.SnapshotPcm()); };
        recordingTimer.Tick += async (_, _) => { recordingTimer.Stop(); if (recorder != null) await ToggleAsync(); };
        historyCleanupTimer.Tick += (_, _) => _ = CleanupHistoryAsync();
        historyCleanupTimer.Start();
        _ = CleanupHistoryAsync();
        SourceInitialized += OnSourceInitialized;
        idleHideTimer.Tick += (_, _) =>
        {
            idleHideTimer.Stop();
            if (PillVisibilityPolicy.ShouldHide(Preferences.Load().HidePillWhenIdle, hotkeyRegistered, recorder != null, busy, settings?.IsVisible == true, notificationArea != null)) Hide();
        };
        Loaded += (_, _) =>
        {
            if (!KeyStore.Exists) ShowSettings();
            else if (startupRegistrationFailed) ShowPill();
            else if (startupLaunch && hotkeyRegistered && notificationArea != null) Hide();
            else ScheduleIdleHide();
        };
        Closed += (_, _) => { notificationArea?.Dispose(); holdShortcut?.Dispose(); idleHideTimer.Stop(); recordingTimer.Stop(); chunkTimer.Stop(); historyCleanupTimer.Stop(); DictationHistory.Invalidate(); lifetime.Cancel(); recordingCancellation?.Cancel(); recorder?.Dispose(); if (hotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, hotkeyId); if (selectionHotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, selectionHotkeyId); if (quickNoteHotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, quickNoteHotkeyId); if (modeHotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, modeHotkeyId); source?.RemoveHook(Hook); groq.Dispose(); lifetime.Dispose(); };
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(Hook);
        // Load once into the encrypted store's in-memory cache; failures are surfaced when used.
        try { PersonalizationStore.Load(); } catch { }
        selectionHotkeyRegistered = Native.RegisterHotKey(source.Handle, selectionHotkeyId, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN | Native.MOD_NOREPEAT, 0x44);
        quickNoteHotkeyRegistered = Native.RegisterHotKey(source.Handle, quickNoteHotkeyId, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN | Native.MOD_NOREPEAT, 0x4E);
        modeHotkeyRegistered = Native.RegisterHotKey(source.Handle, modeHotkeyId, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN | Native.MOD_NOREPEAT, 0x4D);
        bool preferencesReadable = Preferences.TryLoad(out var preferences);
        if (preferencesReadable) { try { StartupRegistration.Set(preferences.StartOnLogin); } catch { startupRegistrationFailed = true; } }
        else startupRegistrationFailed = true;
        currentShortcut = ShortcutBinding.From(preferences);
        string? error = ConfigureShortcut(currentShortcut, true);
        if (error != null) SetStatus("Shortcut unavailable", "Open Settings and choose another shortcut");
        else if (startupRegistrationFailed) SetStatus("Startup unavailable", "Windows startup could not be configured; open Settings");
        else SetStatus("Ready to listen", !selectionHotkeyRegistered || !quickNoteHotkeyRegistered || !modeHotkeyRegistered ? "A secondary shortcut is unavailable; check for conflicts" : shortcutLabel);
    }
    private string? ConfigureShortcut(ShortcutBinding next, bool force = false)
    {
        if (source == null || !next.Valid) return "Choose a valid key along with any desired modifiers.";
        if (!force && (busy || recorder != null || holdSession)) return "Finish the current dictation before changing its shortcut.";
        if (!force && next == currentShortcut && hotkeyRegistered) return null;
        HoldShortcut? newHook = null;
        int nextId = hotkeyId == 1 ? 2 : 1;
        try
        {
            if (next.Hold)
                newHook = new HoldShortcut(next, () => !busy && recorder == null && !holdSession && !shortcutCapture && !recordingQuickNote, () => { var captured = Native.CaptureForeground(); Dispatcher.BeginInvoke(new Action(() => HoldPressed(captured))); }, () => Dispatcher.BeginInvoke(new Action(HoldReleased)));
            else if (!Native.RegisterHotKey(source.Handle, nextId, next.Modifiers | Native.MOD_NOREPEAT, (uint)next.Key))
                return "Windows could not register this shortcut. It may be reserved or already in use; choose another. Your previous shortcut remains active.";
        }
        catch (Exception) { newHook?.Dispose(); return "Windows could not enable hold-to-speak. Your previous shortcut remains active."; }
        if (hotkeyRegistered && !currentShortcut.Hold) Native.UnregisterHotKey(source.Handle, hotkeyId);
        holdShortcut?.Dispose(); holdShortcut = newHook;
        currentShortcut = next; hotkeyId = nextId; hotkeyRegistered = true;
        shortcutLabel = SettingsWindow.ShortcutLabel(next);
        return null;
    }
    private void CaptureChanged(bool capturing)
    {
        shortcutCapture = capturing;
        if (capturing)
        {
            if (source != null && hotkeyRegistered && !currentShortcut.Hold) Native.UnregisterHotKey(source.Handle, hotkeyId);
            holdShortcut?.Dispose(); holdShortcut = null; hotkeyRegistered = false;
        }
        else if (ConfigureShortcut(currentShortcut, true) != null)
        {
            hotkeyRegistered = false; ShowPill(); SetStatus("Shortcut unavailable", "Choose another shortcut in Settings");
        }
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == quickNoteHotkeyId && quickNoteHotkeyRegistered && !shortcutCapture)
        {
            handled = true;
            if (!busy && (recorder == null || recordingQuickNote)) _ = ToggleAsync(default, true);
            else if (recorder != null) SetStatus("Finish dictation first", "Quick Notes is a separate recording");
            return IntPtr.Zero;
        }
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == modeHotkeyId && modeHotkeyRegistered && !shortcutCapture)
        { handled = true; ChangeMode(); return IntPtr.Zero; }
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == selectionHotkeyId && selectionHotkeyRegistered && !shortcutCapture)
        {
            handled = true;
            if (!busy && recorder == null) _ = AddSelectionAsync(Native.CaptureForeground());
            return IntPtr.Zero;
        }
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == hotkeyId && !currentShortcut.Hold && !shortcutCapture) { handled = true; if (recordingQuickNote && recorder != null) { SetStatus("Quick Note recording", "Press the Quick Note shortcut again to finish"); return IntPtr.Zero; } var captured = recorder == null && !busy ? Native.CaptureForeground() : default; _ = ToggleAsync(captured); }
        return IntPtr.Zero;
    }
    private async Task AddSelectionAsync((IntPtr Window, uint ProcessId) captured)
    {
        if (settings?.IsVisible == true)
        {
            IvyDialog.Show("Close Settings before adding a selection, so unsaved edits are not overwritten.", "Ivy Say");
            return;
        }
        if (Interlocked.Exchange(ref selectionLookupActive, 1) != 0) return;
        string? value = null;
        var lookup = Task.Run(() => { try { return SelectionReader.Read(captured); } finally { Interlocked.Exchange(ref selectionLookupActive, 0); } });
        try { value = await lookup.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException) { }
        catch { }
        if (settings?.IsVisible == true) { IvyDialog.Show("Close Settings before adding a selection, so unsaved edits are not overwritten.", "Ivy Say"); return; }
        if (string.IsNullOrEmpty(value)) { IvyDialog.Show("Could not read a short text selection. Select a word or phrase in an accessible text field and try again.", "Ivy Say"); return; }
        if (IvyDialog.Show($"Add this selection to your encrypted dictionary?\n\n{value}\n\nWhen personal words are enabled, it may be sent to Groq.", "Ivy Say", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var data = PersonalizationStore.Load();
            var terms = data.Vocabulary.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Contains(value, StringComparer.OrdinalIgnoreCase)) { IvyDialog.Show("That word is already in your dictionary.", "Ivy Say"); return; }
            data.Vocabulary = string.IsNullOrWhiteSpace(data.Vocabulary) ? value : data.Vocabulary.TrimEnd() + "\n" + value;
            PersonalizationStore.Save(data);
            IvyDialog.Show("Added. Enable personal words in Settings to use it for dictation.", "Ivy Say");
        }
        catch { IvyDialog.Show("Could not add the selection securely. Check that your dictionary is readable and has room.", "Ivy Say"); }
    }
    private async void HoldPressed((IntPtr Window, uint ProcessId) captured)
    {
        if (shortcutCapture || busy || recorder != null || holdSession) return;
        holdSession = true; holdReleased = false;
        await ToggleAsync(captured);
        if (holdReleased && recorder != null && !busy) { holdSession = false; await ToggleAsync(); }
        else if (recorder == null) holdSession = false;
    }
    private void HoldReleased()
    {
        if (!holdSession) return;
        holdReleased = true;
        if (!busy && recorder != null) { holdSession = false; _ = ToggleAsync(); }
        else if (!busy) holdSession = false;
    }

    private async Task ToggleAsync((IntPtr Window, uint ProcessId) captured = default, bool quickNote = false)
    {
        if (busy) return;
        idleHideTimer.Stop();
        if (recorder == null)
        {
            LearningFeedback.Cancel("Learning check stopped because a new dictation started.");
            learningGeneration++; // A new dictation invalidates any pending edit observation.
            if (!KeyStore.Exists) { ShowPill(); ShowSettings(); return; }
            busy = true;
            string startStage = "Setup";
            try
            {
                recordingQuickNote = quickNote;
                var settingsSnapshot = Preferences.Load();
                if (settingsSnapshot.LearnCorrections || settingsSnapshot.LearnAppStyles) LearningFeedback.Finish("No learning check has completed for the current dictation yet.");
                if (quickNote && (!settingsSnapshot.QuickNotesEnabled || string.IsNullOrWhiteSpace(settingsSnapshot.QuickNotesFolder))) { recordingQuickNote = false; ShowPill(); IvyDialog.Show("Enable Quick Notes and choose a folder in Advanced settings first.", "Ivy Say"); return; }
                if (quickNote && !settingsSnapshot.Polish) { recordingQuickNote = false; ShowPill(); IvyDialog.Show("Enable AI formatting before recording a Quick Note.", "Ivy Say"); return; }
                target = captured.Window;
                if (!quickNote && (target == IntPtr.Zero || target == source?.Handle)) throw new InvalidOperationException("Focus the app where you want to type first.");
                targetControl = quickNote ? IntPtr.Zero : Native.FocusedControl(target);
                // Record the user's target before showing the non-activating pill.
                ShowPill();
                settingsTargetElement = null;
                if (!quickNote && settings?.IsVisible == true && new WindowInteropHelper(settings).Handle == target)
                {
                    // Win32 exposes only WPF's top-level HWND, not which WPF control has focus.
                    // Do not dictate into buttons or secret-key fields, and remember the exact editor.
                    if (Keyboard.FocusedElement is not TextBoxBase editor || !editor.IsEnabled || editor.IsReadOnly || settings.IsSecretEditor(editor))
                        throw new InvalidOperationException("Click an editable text box before starting dictation. The API key field cannot be used for dictation.");
                    settingsTargetElement = editor;
                }
                string keySnapshot = KeyStore.Load();
                recordingKey = keySnapshot;
                var personal = (settingsSnapshot.UsePersonalization || settingsSnapshot.UseSnippets || settingsSnapshot.LearnCorrections || settingsSnapshot.LearnAppStyles) ? PersonalizationStore.Load() : new PersonalizationData();
                string vocabulary = string.Join("\n", new[] { settingsSnapshot.UsePersonalization ? personal.Vocabulary : "", settingsSnapshot.LearnCorrections ? personal.LearnedWords : "" }.Where(x => x.Length > 0));
                string hints = vocabulary.Length > 0 ? PersonalizationRules.BuildPrompt(vocabulary, settingsSnapshot.SpellingHints) : settingsSnapshot.SpellingHints;
                recordingPreferences = settingsSnapshot;
                recordingAppName = (settingsSnapshot.UseAppContext || settingsSnapshot.LearnAppStyles) && settingsSnapshot.Polish && captured.ProcessId != 0
                    ? Task.Run(() => Native.ResolveProcessName(captured)) : null;
                recordingPersonal = personal;
                IntPtr contextControl = targetControl;
                recordingNearby = settingsSnapshot.UseNearbyText && settingsSnapshot.Polish && !quickNote && settingsTargetElement == null && captured.ProcessId != 0 && targetControl != IntPtr.Zero
                    ? CorrectionReader.TryAsync(() => NearbyContextReader.Read(captured, contextControl)) : null;
                recordingHistoryGeneration = DictationHistory.Generation;
                recordingHistoryTarget = settingsSnapshot.HistoryEnabled && !quickNote && settingsTargetElement == null && captured.ProcessId != 0 && contextControl != IntPtr.Zero
                    ? HistoryFieldSafety.TryAsync(() => HistoryFieldSafety.Capture(captured, contextControl)) : null;
                recordingCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                chunkSession = new ChunkedTranscription(settingsSnapshot.ChunkThresholdSeconds, settingsSnapshot.CompressionKbps, hints,
                    (audio, opus, prompt, token) => groq.TranscribeAudioAsync(audio, opus, keySnapshot, settingsSnapshot.TranscriptionModel, settingsSnapshot.Language, prompt, token), recordingCancellation.Token);
                startStage = "Microphone";
                recorder = new AudioRecorder(settingsSnapshot.Microphone);
                SetStatus("Preparing microphone…", "Wait for Listening before speaking");
                await recorder.StartAsync(lifetime.Token);
                recordingTimer.Start();
                chunkTimer.Start();
                CancellationToken token = lifetime.Token;
                _ = Task.Run(async () =>
                {
                    try { await groq.WarmConnectionAsync(keySnapshot, token); }
                    catch { /* Warmup is best-effort and must never prevent recording. */ }
                });
                SetStatus(quickNote ? "Note listening…" : "Listening…", quickNote ? "Press Quick Note shortcut again to finish" : currentShortcut.Hold ? "Release your shortcut to finish" : "Press shortcut again to finish");
                StatusSounds.Play(settingsSnapshot.StatusSounds, 680);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex) { StatusSounds.Play(recordingPreferences?.StatusSounds == true, 250); recordingQuickNote = false; recordingCancellation?.Cancel(); recordingCancellation?.Dispose(); recordingCancellation = null; chunkSession = null; recordingPreferences = null; recordingPersonal = null; recordingKey = null; recordingAppName = null; recordingNearby = null; recordingHistoryTarget = null; settingsTargetElement = null; recorder?.Dispose(); recorder = null; ShowPill(); ShowError(ex, startStage); }
            finally { busy = false; if (recorder == null) ScheduleIdleHide(); }
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
        string stage = "Microphone";
        try
        {
            SetStatus("Thinking…", "Transcribing your voice");
            var nearbyTarget = Native.CaptureForeground();
            var nearbyControl = targetControl;
            var capturedNearby = recordingNearby is { IsCompletedSuccessfully: true } ? recordingNearby.Result : null;
            Task<string?>? verifiedNearby = capturedNearby != null && nearbyTarget.Window == target
                ? CorrectionReader.TryAsync(() => NearbyContextReader.Verify(nearbyTarget, nearbyControl, capturedNearby)) : null;
            byte[] wav = await current.StopAsync();
            // Audio is 16 kHz, mono, 16-bit PCM with a 44-byte WAV header.
            TimeSpan recordingDuration = TimeSpan.FromSeconds((wav.Length - 44) / 32000d);
            var preferences = recordingPreferences!;
            var personal = recordingPersonal!;
            string formatterVocabulary = string.Join("\n", new[] { preferences.UsePersonalization ? personal.Vocabulary : "", preferences.LearnCorrections ? personal.LearnedWords : "" }.Where(x => x.Length > 0));
            string key = recordingKey!;
            allowUnsafeCopy = preferences.SecurityLevel == "Unrestricted";
            stage = "Transcription";
            var transcriptionTimer = Stopwatch.StartNew();
            if (preferences.EnableChunking) chunkSession!.Update(wav[44..]);
            string raw = await chunkSession!.FinishAsync(wav);
            if (chunkSession.UsedFullRecordingFallback) SetStatus("Transcribed", "Full recording retry used");
            transcriptionTimer.Stop();
            TimeSpan? formattingTime = null;
            TimeSpan? firstDeltaTime = null;
            string? polishedText = null;
            if (raw.Length == 0) { StatusSounds.Play(preferences.StatusSounds, 250); SetStatus("Nothing heard", "Try speaking a little louder"); return; }
            string corrected = preferences.UsePersonalization ? PersonalizationRules.Parse(personal.Corrections).Apply(raw) : raw;
            string? expansion = null;
            bool isSnippet = preferences.UseSnippets && SnippetRules.Parse(personal.Snippets).TryMatch(raw, out expansion);
            recoverableText = isSnippet ? expansion! : corrected;
            bool structured = System.Text.RegularExpressions.Regex.IsMatch(raw, @"^\s*(code|bullet|bullets)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (isSnippet || (preferences.Polish && preferences.FormattingMode == "structured")) structured = true; // Preserve the exact saved expansion.
            string? nearby = preferences.UseNearbyText && verifiedNearby is { IsCompletedSuccessfully: true } && Native.GetForegroundWindow() == target && Native.FocusedControl(target) == targetControl ? verifiedNearby.Result : null;
            string? sharedNearby = NearbyContext.ForSharing(preferences.UseNearbyText, preferences.ShareNearbyText, nearby);
            bool guardedQuestion = DictationGuard.RequiresGuard(corrected);
            bool protectedOutput = false;
            bool directComplete = false;
            var formatterChoice = FormatterSelection.Select(raw, preferences.SmartFormatterRouting, preferences.FormatterWordThreshold);
            string formattingModel = formatterChoice.Model;
            bool usedFallback = false;
            FirstWordTimer? firstWordTimer = null;
            if (preferences.Polish && !isSnippet)
            {
                SetStatus("Thinking…", "Polishing your words");
                bool direct = NearbyContext.CanStreamDirect(preferences.StreamDirect, allowUnsafeCopy, recordingQuickNote, guardedQuestion, sharedNearby);
                bool directAttempted = false;
                var streamed = new StringBuilder();
                try
                {
                    if (direct)
                    {
                        await WaitForShortcutReleaseAsync(lifetime.Token);
                    }
                    // Never wait for context lookup: omission is preferable to delaying dictation.
                    string? appKey = recordingAppName is { IsCompletedSuccessfully: true } ? recordingAppName.Result : null;
                    string? appName = preferences.UseAppContext ? appKey : null;
                    string? appStyle = preferences.LearnAppStyles ? AppStyleProfiles.Hint(personal.AppStyles, appKey, corrected, preferences.FormattingMode) : null;
                    var formattingTimer = Stopwatch.StartNew();
                    var currentWordTimer = new FirstWordTimer(() => formattingTimer.Elapsed);
                    firstWordTimer = currentWordTimer;
                    string polished = await FormatterRouter.RunAsync(
                        (model, callback, token) => groq.PolishStreamingAsync(corrected, key, model, callback, token, appName, formatterVocabulary, preferences.FormattingMode, appStyle: appStyle, nearby: sharedNearby),
                        async fragment =>
                    {
                        firstDeltaTime ??= formattingTimer.Elapsed;
                        streamed.Append(fragment);
                        int start = Math.Max(0, streamed.Length - 90);
                        string preview = streamed.ToString(start, streamed.Length - start).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                        SetStatus(direct ? "Typing…" : "Polishing…", guardedQuestion ? "Checking your dictation" : preview);
                        if (direct)
                        {
                            await WaitForShortcutReleaseAsync(lifetime.Token);
                            directAttempted = true; // Even a failed call can insert part of a fragment.
                            Native.TypeText(fragment, target, targetControl, currentWordTimer.Observe, ValidateSettingsTarget);
                        }
                    },
                    () =>
                    {
                        usedFallback = true;
                        streamed.Clear();
                        firstDeltaTime = null;
                        SetStatus("Switching formatter…", "Trying another formatter");
                    }, direct, lifetime.Token, formatterChoice.Model, model => formattingModel = model);
                    if (!direct)
                    {
                        var checkedResult = await FormatterFidelity.CheckAsync(corrected, polished, preferences.FormattingMode, async token =>
                        {
                            streamed.Clear();
                            firstDeltaTime = null;
                            SetStatus("Checking dictation…", "Refining your words");
                            return await groq.PolishStreamingAsync(corrected, key, formattingModel, fragment =>
                            {
                                firstDeltaTime ??= formattingTimer.Elapsed;
                                return Task.CompletedTask;
                            }, token, appName, formatterVocabulary, preferences.FormattingMode, strict: true, appStyle: appStyle, nearby: sharedNearby);
                        }, lifetime.Token);
                        if (checkedResult.Rejected)
                        {
                            protectedOutput = true;
                            formattingError = "Formatter output rejected; dictated text preserved. " + checkedResult.Reason;
                            recoverableText = DictationGuard.SafeText(corrected);
                        }
                        else polished = checkedResult.Text!;
                    }
                    formattingTime = formattingTimer.Elapsed;
                    if (!protectedOutput && string.IsNullOrWhiteSpace(polished)) throw new InvalidOperationException("Formatting returned an empty result.");
                    if (direct && !structured && !char.IsWhiteSpace(polished[^1]))
                    {
                        await WaitForShortcutReleaseAsync(lifetime.Token);
                        Native.TypeText(" ", target, targetControl, currentWordTimer.Observe, ValidateSettingsTarget);
                    }
                    if (direct) currentWordTimer.Complete();
                    directComplete = direct;
                    if (!protectedOutput)
                    {
                        polishedText = direct ? polished : polished.Trim();
                        if (!direct && !structured && nearby != null) polishedText = NearbyContext.ApplyCase(polishedText, nearby);
                        recoverableText = polishedText;
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    if (directAttempted)
                    {
                        StatusSounds.Play(preferences.StatusSounds, 250);
                        SetStatus("Stream interrupted", "Check text already inserted");
                        SaveRecovery(streamed.ToString(), "Streaming stopped before completion. Some or all of this partial text may already be in the original app. Check it before copying; do not blindly paste it again.", allowUnsafeCopy);
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
            if (recordingQuickNote)
            {
                if (preferences.LearnCorrections || preferences.LearnAppStyles) LearningFeedback.Finish("Quick Notes do not use correction or app-style learning.");
                if (formattingError != null && !protectedOutput)
                {
                    SetStatus("Note not saved", "Formatting failed; review your text");
                    StatusSounds.Play(preferences.StatusSounds, 250);
                    SaveRecovery(recoverableText!, "Quick Note formatting failed, so nothing was saved. Review and copy if needed.", allowUnsafeCopy);
                    recoverableText = null;
                    return;
                }
                try { await QuickNotes.AppendAsync(preferences.QuickNotesFolder, recoverableText!, DateTimeOffset.Now, lifetime.Token); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    SetStatus("Note save failed", "Check your notes folder");
                    StatusSounds.Play(preferences.StatusSounds, 250);
                    SaveRecovery(recoverableText!, "Quick Note could not be saved completely. The file may contain a partial entry; check it before copying or retrying.", allowUnsafeCopy);
                    recoverableText = null;
                    return;
                }
                _ = UsageStatistics.Default.RecordAsync(recoverableText!, recordingDuration, DateTime.Now);
                recoverableText = null;
                SetStatus("Note saved", "Saved to inbox.md");
                notificationArea?.ShowSavedNote();
                StatusSounds.Play(preferences.StatusSounds, 900);
                return;
            }
            if (directComplete)
            {
                if (preferences.LearnCorrections || preferences.LearnAppStyles) LearningFeedback.Finish("Direct streaming skips learning; choose Wait until the text is ready to learn from edits.");
                _ = UsageStatistics.Default.RecordAsync(polishedText!, recordingDuration, DateTime.Now);
                if (preferences.HistoryEnabled && !isSnippet) _ = SaveHistoryAsync(raw, polishedText!, wav, preferences.HistoryAudioEnabled, recordingHistoryTarget, recordingHistoryGeneration, lifetime.Token);
                recoverableText = null;
                SetCompletionStatus(firstWordTimer, formattingModel, usedFallback);
                StatusSounds.Play(preferences.StatusSounds, 900);
                if (preferences.ShowComparison)
                    new ComparisonWindow(raw, corrected, polishedText, preferences.TranscriptionModel + (chunkSession!.UsedFullRecordingFallback ? " · full WAV retry" : chunkSession.ChunkCount > 0 ? " · chunked" : " · full recording"), transcriptionTimer.Elapsed, formattingTime, firstDeltaTime, formattingError, ModelLabel(formattingModel), usedFallback, preferences.Polish && !isSnippet, preferences.UsePersonalization, wav, preferences.Microphone ?? "Windows default (may change after restart)", formatterChoice.Reason).Show();
                return;
            }
            // In unrestricted mode, skip the extra process lookup and optional review checks.
            var (riskyTarget, targetUnknown) = allowUnsafeCopy ? (false, false) : Native.AssessTarget(target);
            string? reviewReason = InsertionGuard.ReviewReason(recoverableText, riskyTarget, targetUnknown || targetControl == IntPtr.Zero, preferences.SecurityLevel);
            if (reviewReason != null)
            {
                SetStatus("Review before inserting", "Nothing typed automatically");
                if (preferences.LearnCorrections || preferences.LearnAppStyles) LearningFeedback.Finish("Learning skipped because the text was held for security review.");
                new RecoveryWindow(recoverableText, (usedFallback && polishedText != null ? $"{ModelLabel(formattingModel)} fallback was used. " : "") + reviewReason + " Copying and pasting into a terminal can execute commands; verify the text and destination first.").Show();
                recoverableText = null;
                return;
            }
            // Do not inject while modifiers from the shortcut are still held.
            await WaitForShortcutReleaseAsync(lifetime.Token);
            // Separate consecutive prose dictations without adding trailing whitespace to code or lists.
            string insertedText = recoverableText + (structured ? "" : " ");
            CorrectionReader.Snapshot? learningSnapshot = null;
            var learningTarget = Native.CaptureForeground();
            string? learningApp = recordingAppName is { IsCompletedSuccessfully: true } ? recordingAppName.Result : null;
            bool learnStyle = preferences.LearnAppStyles && preferences.Polish && learningApp != null && AppStyleProfiles.Eligible(corrected, preferences.FormattingMode);
            if ((preferences.LearnCorrections || learnStyle) && !isSnippet && !protectedOutput && formattingError == null && settingsTargetElement == null && settings?.IsVisible != true && learningTarget.Window == target && insertedText.Length <= 160)
            {
                try { learningSnapshot = await CorrectionReader.TryAsync(() => CorrectionReader.Before(learningTarget.Window, learningTarget.ProcessId)).WaitAsync(TimeSpan.FromMilliseconds(150), lifetime.Token); }
                catch (TimeoutException) { }
            }
            Native.TypeTextBatch(insertedText, target, targetControl, polishedText != null && firstWordTimer != null ? firstWordTimer.Observe : null, ValidateSettingsTarget);
            if (learningSnapshot != null)
            {
                LearningFeedback.Start();
                _ = ObserveCorrectionAsync(learningTarget, learningSnapshot, insertedText, learningGeneration, preferences.LearnCorrections, learnStyle ? learningApp : null, lifetime.Token);
            }
            else if (preferences.LearnCorrections || preferences.LearnAppStyles)
                LearningFeedback.Finish("Learning skipped: this insertion or field was not eligible or could not be read safely.");
            if (polishedText != null) firstWordTimer?.Complete();
            _ = UsageStatistics.Default.RecordAsync(recoverableText, recordingDuration, DateTime.Now);
            if (preferences.HistoryEnabled && !isSnippet) _ = SaveHistoryAsync(raw, recoverableText, wav, preferences.HistoryAudioEnabled, recordingHistoryTarget, recordingHistoryGeneration, lifetime.Token);
            recoverableText = null;
            StatusSounds.Play(preferences.StatusSounds, 900);
            if (protectedOutput) SetStatus("Dictation preserved", "Formatter output was rejected");
            else if (formattingError != null) SetStatus(preferences.UsePersonalization ? "Personalized text inserted" : "Raw transcript inserted", usedFallback ? $"{formattingError} · {ModelLabel(formattingModel)} failed" : $"{formattingError} · formatting failed");
            else if (isSnippet) SetStatus("Snippet inserted", "AI formatting skipped");
            else if (polishedText != null) SetCompletionStatus(firstWordTimer, formattingModel, usedFallback);
            else SetStatus(preferences.UsePersonalization ? "Personalized text" : "Raw mode", shortcutLabel);
            if (preferences.ShowComparison)
                new ComparisonWindow(raw, corrected, polishedText, preferences.TranscriptionModel + (chunkSession!.UsedFullRecordingFallback ? " · full WAV retry" : chunkSession.ChunkCount > 0 ? " · chunked" : " · full recording"), transcriptionTimer.Elapsed, formattingTime, firstDeltaTime, formattingError, ModelLabel(formattingModel), usedFallback, preferences.Polish && !isSnippet, preferences.UsePersonalization, wav, preferences.Microphone ?? "Windows default (may change after restart)", formatterChoice.Reason).Show();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            StatusSounds.Play(recordingPreferences?.StatusSounds == true, 250);
            if (recoverableText != null)
            {
                if (recordingPreferences?.LearnCorrections == true || recordingPreferences?.LearnAppStyles == true) LearningFeedback.Finish("Learning skipped because insertion did not complete.");
                SetStatus("Review dictation", "Text was not fully inserted");
                SaveRecovery(recoverableText, "Text could not be fully inserted into the original window. Some text may already have been inserted.", allowUnsafeCopy);
            }
            else ShowError(ex, stage);
        }
        finally { recordingCancellation?.Cancel(); recordingCancellation?.Dispose(); recordingCancellation = null; chunkSession = null; recordingPreferences = null; recordingPersonal = null; recordingKey = null; recordingAppName = null; recordingNearby = null; recordingHistoryTarget = null; recordingQuickNote = false; settingsTargetElement = null; current.Dispose(); busy = false; ScheduleIdleHide(); }
    }

    private void SaveRecovery(string text, string explanation, bool allowUnsafeCopy)
    {
        pendingRecovery = (text, explanation, allowUnsafeCopy);
        ShowPill();
        SetStatus("Text needs attention", "Right-click Ivy Say and choose Recover last dictation");
        notificationArea?.ShowRecoveryAvailable();
    }
    private void RecoverLastClicked(object sender, RoutedEventArgs e) => RecoverLast();
    private void RecoverLast()
    {
        if (pendingRecovery is not { } item) { ShowPill(); SetStatus("Nothing to recover", "No failed dictation is held in memory"); return; }
        new RecoveryWindow(item.Text, item.Explanation, item.AllowUnsafeCopy).Show();
    }

    private async Task WaitForShortcutReleaseAsync(CancellationToken token)
    {
        while (Native.ShortcutHeld(currentShortcut, currentShortcut.Hold ? holdShortcut?.TriggerHeld : null))
        {
            SetStatus("Release keys…", "Text will be inserted when the shortcut is released");
            await Task.Delay(30, token);
        }
    }

    private static async Task CleanupHistoryAsync()
    {
        try { await DictationHistory.CleanupAsync(Preferences.Load().HistoryDays); }
        catch { /* Retention cleanup is retried later; never interrupt dictation. */ }
    }
    private async Task SaveHistoryAsync(string raw, string result, byte[] wav, bool includeAudio, Task<HistoryFieldSafety.Target?>? captured, long generation, CancellationToken token)
    {
        try
        {
            if (captured is not { IsCompletedSuccessfully: true } || captured.Result == null) return;
            var target = await HistoryFieldSafety.TryAsync(() => HistoryFieldSafety.Verify(captured.Result)).WaitAsync(TimeSpan.FromSeconds(2), token);
            if (target == null || token.IsCancellationRequested) return;
            if (!await DictationHistory.RecordAsync(raw, result, wav, includeAudio, generation) && !token.IsCancellationRequested && recorder == null && !busy)
                SetStatus("Dictation inserted", "Could not save history");
        }
        catch { if (!token.IsCancellationRequested && recorder == null && !busy) SetStatus("Dictation inserted", "Could not save history"); }
    }

    private void ValidateSettingsTarget()
    {
        if (settingsTargetElement != null)
            SettingsTargetGuard.Validate(settingsTargetElement, Keyboard.FocusedElement, settings?.IsVisible == true, settings?.IsActive == true);
    }

    private async Task ObserveCorrectionAsync((IntPtr Window, uint ProcessId) captured, CorrectionReader.Snapshot snapshot, string inserted, int generation, bool learnWords, string? app, CancellationToken token)
    {
        try
        {
            // Confirm that the exact inserted text is present before considering any later edit.
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            string? initial = await CorrectionReader.TryAsync(() => CorrectionReader.After(captured.Window, captured.ProcessId, snapshot)).WaitAsync(TimeSpan.FromSeconds(1), token);
            if (generation != learningGeneration) return;
            if (initial != inserted) { LearningFeedback.Finish("Learning skipped: Ivy Say could not verify the inserted text in the same field after one second."); return; }
            await Task.Delay(TimeSpan.FromSeconds(24), token);
            string? edited = await CorrectionReader.TryAsync(() => CorrectionReader.After(captured.Window, captured.ProcessId, snapshot)).WaitAsync(TimeSpan.FromSeconds(1), token);
            if (generation != learningGeneration || settings?.IsVisible == true) return;
            if (edited == null) { LearningFeedback.Finish("Learning skipped: the same text field or insertion could not be read at the final check."); return; }
            var current = Preferences.Load();
            bool useWords = learnWords && current.LearnCorrections;
            bool useStyle = app != null && current.LearnAppStyles;
            if (!useWords && !useStyle) { LearningFeedback.Finish("Learning was disabled before the check finished."); return; }
            var data = PersonalizationStore.Load();
            bool wordChanged = false;
            string? candidate = useWords ? CorrectionLearning.Candidate(inserted, edited) : null;
            if (candidate != null)
            {
                string next = CorrectionLearning.Add(data.LearnedWords, candidate);
                wordChanged = next != data.LearnedWords;
                data.LearnedWords = next;
            }
            bool styleChanged = useStyle && AppStyleProfiles.Observe(data.AppStyles, app, inserted, edited);
            if (wordChanged || styleChanged)
            {
                PersonalizationStore.Save(data);
                LearningFeedback.Finish(wordChanged && styleChanged ? "A word was saved and an app-style signal was recorded." : wordChanged ? "A word was saved. See Learned words below." : "An app-style signal was recorded. Several consistent changes are needed before it is used.");
            }
            else LearningFeedback.Finish(candidate != null ? "That word is already learned or the learned-word list is full." : "Check completed: no supported single-word spelling or app-style edit was found.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (generation == learningGeneration) LearningFeedback.Finish("Learning check could not complete or save. Your dictation was not affected."); }
    }
    private static string ModelLabel(string model) => model == FormatterRouter.LastResort ? "Backup formatter" : model == FormatterRouter.Fallback ? "GPT-OSS 120B" : "GPT-OSS 20B";
    private void SetCompletionStatus(FirstWordTimer? timer, string model, bool usedFallback)
    {
        string detail = (model == FormatterRouter.LastResort ? "Backup formatter" : model == FormatterRouter.Fallback ? "120B" : "20B") + (usedFallback ? " fallback" : "");
        if (timer?.FirstWord is TimeSpan elapsed) SetStatus($"Done in {elapsed.TotalSeconds:F1}s", $"{detail} · AI → first word");
        else SetStatus("AI formatted", detail);
    }
    private void SetStatus(string title, string detail) { StatusTitle.Text = title; StatusTitle.Tag = detail; }
    private void ShowPill() { if (!IsVisible) Show(); }
    private void ScheduleIdleHide()
    {
        idleHideTimer.Stop();
        // Never hide an unavailable shortcut: the user needs the pill to reach Settings.
        if (PillVisibilityPolicy.ShouldHide(Preferences.Load().HidePillWhenIdle, hotkeyRegistered, recorder != null, busy, settings?.IsVisible == true, notificationArea != null)) idleHideTimer.Start();
    }
    private void QuitPill(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void ShowError(Exception ex, string stage)
    {
        string message = FailureDetails.Explain(ex, stage);
        SetStatus(stage + " issue", message);
        IvyDialog.Show(message, "Ivy Say · " + stage, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void CycleMode(object sender, RoutedEventArgs e) => ChangeMode();
    private void ChangeMode()
    {
        if (busy || recorder != null) { ShowPill(); SetStatus("Finish dictation first", "Style applies to the next recording"); return; }
        try
        {
            var preferences = Preferences.Load();
            preferences.FormattingMode = FormattingModes.Next(preferences.FormattingMode);
            preferences.Save();
            ShowPill(); SetStatus("Style: " + FormattingModes.Label(preferences.FormattingMode), "Applies when AI formatting is enabled"); ScheduleIdleHide();
        }
        catch { IvyDialog.Show("Could not save formatting style.", "Ivy Say"); }
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettings();
    private void ShowSettings()
    {
        if (recorder != null || busy) { ShowPill(); SetStatus("Finish dictation first", "Press shortcut to stop recording"); return; }
        if (settings?.IsVisible == true) { settings.Activate(); return; }
        LearningFeedback.Cancel("Learning check stopped because Settings opened before the 25-second check.");
        learningGeneration++;
        idleHideTimer.Stop();
        settings = new SettingsWindow();
        settings.CanCaptureShortcut = () => !busy && recorder == null && !holdSession;
        settings.ShortcutCaptureChanged = CaptureChanged;
        settings.ApplyShortcut = binding => ConfigureShortcut(binding);
        settings.PreferencesSaved += (_, _) => { if (!Preferences.Load().HidePillWhenIdle) { idleHideTimer.Stop(); ShowPill(); } };
        settings.Closed += (_, _) => Dispatcher.BeginInvoke(new Action(ScheduleIdleHide));
        settings.Show();
        settings.Activate();
    }
    private void DragPill(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
}
