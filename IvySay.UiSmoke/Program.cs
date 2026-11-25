using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IvySay;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Run with a temporary LOCALAPPDATA supplied by CI: never touch a real user's data.
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new App();
        app.InitializeComponent();
        var window = new SettingsWindow();
        window.Show();
        int result = 1;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                Check(window, "General");
                if (((CheckBox)window.FindName("StartOnLogin")).IsChecked != true) throw new Exception("Startup should be enabled by default");
                CheckStartupRegistration();
                if (((ComboBox)window.FindName("SpokenLanguage")).SelectedIndex != 1 || ((ComboBox)window.FindName("SecurityLevel")).SelectedIndex != 0 || ((CheckBox)window.FindName("Polish")).IsChecked != true) throw new Exception("New installation general defaults are incorrect");
                if (AppCursors.Arrow == System.Windows.Input.Cursors.Arrow || AppCursors.Hand == System.Windows.Input.Cursors.Hand)
                    throw new Exception("Custom cursor resource failed to load");
                CheckCardMotion((Border)window.FindName("ConnectionCard"));
                CheckKineticGrid((KineticGrid)window.FindName("KineticBackground"));
                var save = (Button)window.FindName("GeneralSettingsSave");
                save.ApplyTemplate();
                if (save.Template.FindName("Shine", save) is not Border) throw new Exception("Button glow missing");
                CheckNotificationArea();
                var language = (ComboBox)window.FindName("SpokenLanguage");
                language.IsDropDownOpen = true;
                window.UpdateLayout();
                language.IsDropDownOpen = false;
                window.UpdateLayout();
                var tabs = (TabControl)window.FindName("SettingsTabs");
                tabs.SelectedIndex = 1;
                window.UpdateLayout();
                Check(window, "My words");
                if (((TextBlock)window.FindName("LearningStatus")).Text.Length == 0) throw new Exception("Learning feedback missing");
                if (((CheckBox)window.FindName("LearnCorrections")).IsChecked != false || ((CheckBox)window.FindName("LearnAppStyles")).IsChecked != false) throw new Exception("Correction learning must be opt-in");
                var assembly = typeof(SettingsWindow).Assembly;
                var dataType = assembly.GetType("IvySay.PersonalizationData")!;
                var storeType = assembly.GetType("IvySay.PersonalizationStore")!;
                var profile = Activator.CreateInstance(dataType)!;
                dataType.GetProperty("LearnedWords")!.SetValue(profile, "Prabhjot");
                var styles = dataType.GetProperty("AppStyles")!.GetValue(profile)!;
                var styleType = assembly.GetType("IvySay.AppStyleProfiles")!;
                for (int i = 0; i < 3; i++) styleType.GetMethod("Observe")!.Invoke(null, new object?[] { styles, "notepad", "Hello.", "Hello" });
                storeType.GetMethod("Save")!.Invoke(null, new[] { profile });
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "personalization.dat");
                if (System.Text.Encoding.UTF8.GetString(System.IO.File.ReadAllBytes(path)).Contains("Prabhjot")) throw new Exception("Personalization was stored in plaintext");
                storeType.GetField("cached", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, null);
                var restored = storeType.GetMethod("Load")!.Invoke(null, null)!;
                if ((string?)dataType.GetProperty("LearnedWords")!.GetValue(restored) != "Prabhjot") throw new Exception("Learned words did not round-trip");
                var savedStyles = dataType.GetProperty("AppStyles")!.GetValue(restored)!;
                var hint = styleType.GetMethod("Hint")!.Invoke(null, new object?[] { savedStyles, "notepad", "hello", "clean" });
                if ((string?)hint != "Omit the final period on ordinary statements.") throw new Exception("App style did not round-trip");
                storeType.GetMethod("Delete")!.Invoke(null, null);
                var vocabulary = (TextBox)window.FindName("Vocabulary");
                vocabulary.ApplyTemplate();
                if (vocabulary.Template.FindName("PART_ContentHost", vocabulary) == null || vocabulary.Template.FindName("Shine", vocabulary) == null) throw new Exception("Text field template missing content host or glow");
                CheckGreenCard((Border)window.FindName("StatsCard"));
                tabs.SelectedIndex = 2;
                window.UpdateLayout();
                Check(window, "Advanced");
                if (((CheckBox)window.FindName("HistoryEnabled")).IsChecked != false || ((CheckBox)window.FindName("HistoryAudioEnabled")).IsChecked != false) throw new Exception("History must be opt-in");
                CheckHistoryStorage();
                var recovery = new RecoveryWindow("sample", "Test"); recovery.Show(); recovery.UpdateLayout();
                var recoveryText = (TextBox)recovery.FindName("ResultText"); recoveryText.ApplyTemplate();
                if (recovery.WindowStyle != WindowStyle.None || recoveryText.Template.FindName("Shine", recoveryText) == null) throw new Exception("Recovery styling missing");
                recovery.Close();
                var historyWindow = new HistoryWindow(); historyWindow.Show(); historyWindow.UpdateLayout(); historyWindow.Close();
                if (((CheckBox)window.FindName("UseNearbyText")).IsChecked != false || ((CheckBox)window.FindName("ShareNearbyText")).IsChecked != false) throw new Exception("Nearby context must be opt-in");
                if (((ComboBox)window.FindName("Model")).SelectedIndex != 1 || ((ComboBox)window.FindName("Compression")).SelectedIndex != 1 || ((TextBox)window.FindName("FormatterWordThreshold")).Text != "40" || ((CheckBox)window.FindName("UseAppContext")).IsChecked != true || ((CheckBox)window.FindName("StatusSounds")).IsChecked != true || window.FindName("WarmConnection") != null) throw new Exception("New installation advanced defaults are incorrect");
                CheckOrb();
                CheckHoldEvents();
                CheckDialog(MessageBoxResult.No, "NoButton");
                CheckDialog(MessageBoxResult.Yes, "YesButton");
                CheckDialog(MessageBoxResult.OK, "OkButton");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { window.Close(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal); }
        };
        timer.Start();
        Dispatcher.Run();
        return result;
    }
    private static void CheckStartupRegistration()
    {
        var type = typeof(SettingsWindow).Assembly.GetType("IvySay.StartupRegistration")!;
        string subkey = @"Software\IvySaySmokeTests\" + Guid.NewGuid().ToString("N");
        string temp = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "startup-smoke");
        System.IO.Directory.CreateDirectory(temp);
        string app = System.IO.Path.Combine(temp, "IvySay.exe");
        System.IO.File.WriteAllText(app, "test file");
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(subkey, true)) key.SetValue("IvySay", "original");
            var old = type.GetMethod("Read")!.Invoke(null, new object[] { subkey })!;
            type.GetMethod("Set")!.Invoke(null, new object?[] { true, app, subkey });
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subkey))
                if ((string?)key!.GetValue("IvySay") != "\"" + app + "\" --startup") throw new Exception("Incorrect startup command");
            type.GetMethod("Restore")!.Invoke(null, new object[] { old, subkey });
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subkey))
                if ((string?)key!.GetValue("IvySay") != "original") throw new Exception("Startup rollback failed");
            type.GetMethod("Set")!.Invoke(null, new object?[] { false, null, subkey });
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subkey))
                if (key!.GetValue("IvySay") != null) throw new Exception("Startup disabling failed");
            try { type.GetMethod("Set")!.Invoke(null, new object?[] { true, System.IO.Path.Combine(temp, "dotnet.exe"), subkey }); throw new Exception("Invalid startup executable accepted"); }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { }
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(subkey, false); System.IO.Directory.Delete(temp, true); }
    }
    private static void CheckHistoryStorage()
    {
        var assembly = typeof(SettingsWindow).Assembly;
        var prefsType = assembly.GetType("IvySay.Preferences")!;
        var store = assembly.GetType("IvySay.DictationHistory")!;
        var prefs = Activator.CreateInstance(prefsType)!;
        prefsType.GetProperty("HistoryEnabled")!.SetValue(prefs, true);
        prefsType.GetProperty("HistoryAudioEnabled")!.SetValue(prefs, true);
        prefsType.GetMethod("Save")!.Invoke(prefs, null);
        long generation = (long)store.GetProperty("Generation")!.GetValue(null)!;
        var save = store.GetMethod("RecordAsync")!.Invoke(null, new object[] { "private transcript marker", "private result marker", new byte[44], true, generation })!;
        ((Task)save).GetAwaiter().GetResult();
        if (!(bool)save.GetType().GetProperty("Result")!.GetValue(save)!) throw new Exception("History save failed");
        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay", "history");
        var files = System.IO.Directory.GetFiles(dir, "*.dat");
        if (files.Length != 1 || System.Text.Encoding.UTF8.GetString(System.IO.File.ReadAllBytes(files[0])).Contains("private transcript marker")) throw new Exception("History must be encrypted");
        var listTask = store.GetMethod("ListAsync")!.Invoke(null, new object[] { 30 })!;
        ((Task)listTask).GetAwaiter().GetResult();
        var list = (System.Collections.IList)listTask.GetType().GetProperty("Result")!.GetValue(listTask)!;
        if (list.Count != 1) throw new Exception("History did not round-trip");
        var entry = list[0]!;
        if (!(bool)entry.GetType().GetProperty("HasAudio")!.GetValue(entry)!) throw new Exception("History audio missing");
        ((Task)store.GetMethod("ClearAudioAsync")!.Invoke(null, null)!).GetAwaiter().GetResult();
        var read = store.GetMethod("GetAsync")!.Invoke(null, new[] { entry.GetType().GetProperty("Id")!.GetValue(entry)! })!;
        ((Task)read).GetAwaiter().GetResult();
        var withoutAudio = read.GetType().GetProperty("Result")!.GetValue(read)!;
        if (withoutAudio.GetType().GetProperty("Audio")!.GetValue(withoutAudio) != null) throw new Exception("Saved audio not deleted");
        ((Task)store.GetMethod("ClearAsync")!.Invoke(null, null)!).GetAwaiter().GetResult();
        if (System.IO.Directory.GetFiles(dir, "*.dat").Length != 0) throw new Exception("History not deleted");
        var stale = store.GetMethod("RecordAsync")!.Invoke(null, new object[] { "old", "old", new byte[44], false, generation })!;
        ((Task)stale).GetAwaiter().GetResult();
        if ((bool)stale.GetType().GetProperty("Result")!.GetValue(stale)!) throw new Exception("A cleared pending record reappeared");
        prefsType.GetProperty("HistoryEnabled")!.SetValue(prefs, false); prefsType.GetProperty("HistoryAudioEnabled")!.SetValue(prefs, false); prefsType.GetMethod("Save")!.Invoke(prefs, null);
    }
    private static void CheckKineticGrid(KineticGrid grid)
    {
        if (grid.IsHitTestVisible || grid.ActualWidth < 100 || grid.ActualHeight < 100) throw new Exception("Kinetic background must fill the window without intercepting input");
        var type = typeof(KineticGrid);
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
        {
            type.GetField("mouse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(grid, new Point(grid.ActualWidth / 2, grid.ActualHeight / 2));
            type.GetField("inside", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(grid, true);
        }
        grid.InvalidateVisual(); grid.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)grid.ActualWidth, (int)grid.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(grid);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        int background = 0, lines = 0, blue = 0;
        for (int i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i] == 24 && bytes[i + 1] == 22 && bytes[i + 2] == 22) background++;
            if (bytes[i] > 35 && Math.Abs(bytes[i + 1] - bytes[i + 2]) < 3 && Math.Abs(bytes[i] - bytes[i + 1]) < 3) lines++;
            if (bytes[i] > bytes[i + 1] + 25 && bytes[i + 1] > bytes[i + 2] + 20) blue++;
        }
        if (background < 1000 || lines < 100) throw new Exception($"Kinetic grid backdrop or lines missing: {background}/{lines}");
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast && blue < 30) throw new Exception($"Kinetic grid active blue nodes missing: {blue}");
    }
    private static void CheckHoldEvents()
    {
        var assembly = typeof(SettingsWindow).Assembly;
        var bindingType = assembly.GetType("IvySay.ShortcutBinding")!;
        object binding = Activator.CreateInstance(bindingType, 0x78, 0u, true)!;
        var hookType = assembly.GetType("IvySay.HoldShortcut")!;
        int starts = 0, stops = 0;
        using var hook = (IDisposable)Activator.CreateInstance(hookType, binding, new Func<bool>(() => true), new Action(() => starts++), new Action(() => stops++))!;
        var onKey = hookType.GetMethod("OnKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        IntPtr data = System.Runtime.InteropServices.Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i++) System.Runtime.InteropServices.Marshal.WriteByte(data, i, 0);
            System.Runtime.InteropServices.Marshal.WriteInt32(data, 0, 0x78);
            IntPtr Fire(int message) => (IntPtr)onKey.Invoke(hook, new object[] { 0, new IntPtr(message), data })!;
            var held = hookType.GetProperty("TriggerHeld")!;
            if (Fire(0x100) != new IntPtr(1) || !(bool)held.GetValue(hook)! || Fire(0x100) != new IntPtr(1) || Fire(0x101) != new IntPtr(1) || (bool)held.GetValue(hook)! || starts != 1 || stops != 1)
                throw new Exception("Hold trigger repeat/release handling failed");
            System.Runtime.InteropServices.Marshal.WriteInt32(data, 8, 0x10);
            Fire(0x100); Fire(0x101);
            if (starts != 1 || stops != 1) throw new Exception("Injected input triggered hold-to-speak");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(data); }
        object modifiedBinding = Activator.CreateInstance(bindingType, 0x78, 2u, true)!;
        int released = 0;
        using var second = (IDisposable)Activator.CreateInstance(hookType, modifiedBinding, new Func<bool>(() => true), new Action(() => { }), new Action(() => released++))!;
        hookType.GetField("down", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(second, true);
        hookType.GetField("suppressTrigger", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(second, true);
        data = System.Runtime.InteropServices.Marshal.AllocHGlobal(32);
        try
        {
            for (int i = 0; i < 32; i++) System.Runtime.InteropServices.Marshal.WriteByte(data, i, 0);
            System.Runtime.InteropServices.Marshal.WriteInt32(data, 0, 0xA2);
            onKey.Invoke(second, new object[] { 0, new IntPtr(0x101), data });
            if (released != 1 || !(bool)hookType.GetProperty("TriggerHeld")!.GetValue(second)!) throw new Exception("Trigger must remain held when modifier releases first");
            System.Runtime.InteropServices.Marshal.WriteInt32(data, 0, 0x78);
            onKey.Invoke(second, new object[] { 0, new IntPtr(0x101), data });
            if ((bool)hookType.GetProperty("TriggerHeld")!.GetValue(second)!) throw new Exception("Trigger release not recorded");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(data); }
    }
    private static void CheckCardMotion(Border card)
    {
        if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
        if (card.Background is not DrawingBrush { Drawing: DrawingGroup group } ||
            group.Children[1] is not GeometryDrawing { Brush: RadialGradientBrush glow } || !glow.HasAnimatedProperties)
            throw new Exception("Card glow animation was not started");
    }
    private static void CheckNotificationArea()
    {
        var type = typeof(SettingsWindow).Assembly.GetType("IvySay.NotificationArea") ?? throw new Exception("Notification area is missing");
        bool opened = false, quit = false;
        using var tray = (IDisposable)(Activator.CreateInstance(type, new Action(() => opened = true), new Action(() => quit = true)) ?? throw new Exception("Notification area could not be constructed"));
        var menu = (System.Windows.Forms.ContextMenuStrip)(type.GetField("menu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(tray) ?? throw new Exception("Notification menu missing"));
        menu.Items[0].PerformClick(); menu.Items[1].PerformClick();
        if (!opened || !quit) throw new Exception("Notification area actions did not fire");
    }
    private static void CheckOrb()
    {
        var orb = new ThinkingOrb { Width = 56, Height = 56 };
        orb.Measure(new Size(56, 56));
        orb.Arrange(new Rect(0, 0, 56, 56));
        orb.UpdateLayout();
        var bitmap = new RenderTargetBitmap(56, 56, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(orb);
        var bytes = new byte[56 * 56 * 4];
        bitmap.CopyPixels(bytes, 56 * 4, 0);
        int visible = 0;
        for (int i = 0; i < bytes.Length; i += 4) if (bytes[i + 3] > 15) visible++;
        if (visible < 100) throw new Exception("Thinking orb failed to render");
    }
    private static void CheckGreenCard(Border card)
    {
        if (card.ActualWidth < 100 || card.ActualHeight < 60) throw new Exception("Stats card did not lay out");
        var bitmap = new RenderTargetBitmap((int)card.ActualWidth, (int)card.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(card);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        int green = 0;
        for (int i = 0; i < bytes.Length; i += 4)
            if (bytes[i + 3] > 0 && bytes[i + 1] > bytes[i + 2] + 8 && bytes[i + 1] > bytes[i] + 2) green++;
        Console.WriteLine($"Stats card: {green} green pixels");
        if (green < 100) throw new Exception("Stats card green glow was not rendered");
    }
    private static void CheckDialog(MessageBoxResult expected, string buttonName)
    {
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<IvyDialog>().Single();
            Check(dialog, "Dialog");
            ((Button)dialog.FindName(buttonName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        var actual = IvyDialog.Show("Test confirmation", "Ivy Say", expected == MessageBoxResult.OK ? MessageBoxButton.OK : MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (actual != expected) throw new Exception($"Dialog returned {actual} instead of {expected}");
    }
    private static void Check(Window window, string tab)
    {
        window.UpdateLayout();
        if (window.ActualWidth < 100 || window.ActualHeight < 100 || window.Content is not FrameworkElement content || content.ActualWidth < 100)
            throw new Exception($"{tab}: window content did not lay out");
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        int dark = 0, light = 0;
        for (int i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i + 3] > 0 && bytes[i] < 65 && bytes[i + 1] < 65 && bytes[i + 2] < 65) dark++;
            if (bytes[i + 3] > 0 && bytes[i] > 150 && bytes[i + 1] > 150 && bytes[i + 2] > 150) light++;
        }
        Console.WriteLine($"{tab}: {bitmap.PixelWidth}x{bitmap.PixelHeight}, dark pixels {dark}, light pixels {light}");
        if (dark < 10000 || light < 40) throw new Exception($"{tab}: expected visible dark settings with text");
    }
}
