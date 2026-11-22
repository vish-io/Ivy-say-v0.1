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
                CheckCardMotion((Border)window.FindName("ConnectionCard"));
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
                CheckGreenCard((Border)window.FindName("StatsCard"));
                tabs.SelectedIndex = 2;
                window.UpdateLayout();
                Check(window, "Advanced");
                CheckOrb();
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
