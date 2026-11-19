using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Diagnostics;

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
                recorder = new AudioRecorder(Preferences.Load().Microphone);
                recorder.Start();
                recordingTimer.Start();
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
        bool formattingFailed = false;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            SetStatus("Thinking…", "Transcribing your voice");
            byte[] wav = await current.StopAsync();
            var preferences = Preferences.Load();
            string key = KeyStore.Load();
            string raw = await groq.TranscribeAsync(wav, key, preferences.TranscriptionModel, lifetime.Token);
            if (raw.Length == 0) { SetStatus("Nothing heard", "Try speaking a little louder"); return; }
            recoverableText = raw;
            if (preferences.Polish)
            {
                SetStatus("Thinking…", "Polishing your words");
                try
                {
                    string polished = await groq.PolishAsync(raw, key, lifetime.Token);
                    if (string.IsNullOrWhiteSpace(polished)) throw new InvalidOperationException("Formatting returned an empty result.");
                    recoverableText = polished;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { throw; }
                catch
                {
                    // Preserve the dictation when formatting is unavailable; it is still subject
                    // to the same foreground and focus checks as polished text.
                    formattingFailed = true;
                }
            }
            // Do not inject while modifiers from the shortcut are still held.
            for (int i = 0; i < 100 && Native.ShortcutHeld(); i++) await Task.Delay(30, lifetime.Token);
            if (Native.ShortcutHeld()) throw new InvalidOperationException("The shortcut keys were still held.");
            Native.TypeText(recoverableText, target, targetControl);
            recoverableText = null;
            if (formattingFailed) SetStatus("Raw transcript inserted", "Formatting failed · check your text");
            else SetStatus("Ready to listen", $"Done in {stopwatch.Elapsed.TotalSeconds:F1}s · {shortcutLabel}");
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
