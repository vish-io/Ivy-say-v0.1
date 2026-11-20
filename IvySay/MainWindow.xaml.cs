using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace IvySay;
public partial class MainWindow : Window
{
    private readonly GroqClient groq = new();
    private readonly CancellationTokenSource lifetime = new();
    private AudioRecorder? recorder;
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
        recordingTimer.Tick += async (_, _) => { recordingTimer.Stop(); if (recorder != null) await ToggleAsync(); };
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { if (!KeyStore.Exists) ShowSettings(); };
        Closed += (_, _) => { recordingTimer.Stop(); lifetime.Cancel(); recorder?.Dispose(); if (hotkeyRegistered && source != null) Native.UnregisterHotKey(source.Handle, 1); source?.RemoveHook(Hook); groq.Dispose(); lifetime.Dispose(); };
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
                recorder = new AudioRecorder(settingsSnapshot.Microphone);
                recorder.Start();
                recordingTimer.Start();
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
            catch (Exception ex) { recorder?.Dispose(); recorder = null; ShowError(ex); }
            return;
        }
        recordingTimer.Stop();
        busy = true;
        var current = recorder;
        recorder = null;
        string? recoverableText = null;
        string? formattingError = null;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            SetStatus("Thinking…", "Transcribing your voice");
            byte[] wav = await current.StopAsync();
            var preferences = Preferences.Load();
            string key = KeyStore.Load();
            var transcriptionTimer = Stopwatch.StartNew();
            string raw = await groq.TranscribeAsync(wav, key, preferences.TranscriptionModel, preferences.Language, preferences.SpellingHints, lifetime.Token);
            transcriptionTimer.Stop();
            TimeSpan? formattingTime = null;
            string? polishedText = null;
            if (raw.Length == 0) { SetStatus("Nothing heard", "Try speaking a little louder"); return; }
            recoverableText = raw;
            if (preferences.Polish)
            {
                SetStatus("Thinking…", "Polishing your words");
                try
                {
                    var formattingTimer = Stopwatch.StartNew();
                    string polished = await groq.PolishAsync(raw, key, lifetime.Token);
                    formattingTimer.Stop();
                    formattingTime = formattingTimer.Elapsed;
                    if (string.IsNullOrWhiteSpace(polished)) throw new InvalidOperationException("Formatting returned an empty result.");
                    polishedText = polished;
                    recoverableText = polished;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // Never display or log the provider response, transcript, or API key.
                    formattingError = ex switch
                    {
                        HttpRequestException { StatusCode: not null } httpError => $"Groq HTTP {(int)httpError.StatusCode.Value}",
                        HttpRequestException => "Network error",
                        OperationCanceledException => "Request timed out",
                        JsonException or KeyNotFoundException => "Invalid Groq response",
                        InvalidOperationException => "Empty or incomplete result",
                        _ => "Formatting error"
                    };
                }
            }
            // Do not inject while modifiers from the shortcut are still held.
            for (int i = 0; i < 100 && Native.ShortcutHeld(); i++) await Task.Delay(30, lifetime.Token);
            if (Native.ShortcutHeld()) throw new InvalidOperationException("The shortcut keys were still held.");
            // Separate consecutive prose dictations without adding trailing whitespace to code or lists.
            bool structured = System.Text.RegularExpressions.Regex.IsMatch(raw, @"^\s*(code|bullet|bullets)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            Native.TypeText(recoverableText + (structured ? "" : " "), target, targetControl);
            recoverableText = null;
            if (formattingError != null) SetStatus("Raw transcript inserted", $"{formattingError} · formatting failed");
            else if (polishedText != null) SetStatus("AI formatted", $"Done in {stopwatch.Elapsed.TotalSeconds:F1}s · {shortcutLabel}");
            else SetStatus("Raw mode", $"Done in {stopwatch.Elapsed.TotalSeconds:F1}s · {shortcutLabel}");
            if (preferences.ShowComparison)
                new ComparisonWindow(raw, polishedText, preferences.TranscriptionModel, transcriptionTimer.Elapsed, formattingTime, formattingError, preferences.Polish).Show();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (recoverableText != null)
            {
                SetStatus("Review dictation", "Text was not fully inserted");
                new RecoveryWindow(recoverableText, "Text could not be fully inserted into the original window. Some text may already have been inserted.").Show();
            }
            else ShowError(ex);
        }
        finally { current.Dispose(); busy = false; }
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
