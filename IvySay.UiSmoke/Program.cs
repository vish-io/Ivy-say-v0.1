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
                var language = (ComboBox)window.FindName("SpokenLanguage");
                language.IsDropDownOpen = true;
                window.UpdateLayout();
                language.IsDropDownOpen = false;
                window.UpdateLayout();
                var tabs = (TabControl)window.FindName("SettingsTabs");
                tabs.SelectedIndex = 1;
                window.UpdateLayout();
                Check(window, "My words");
                tabs.SelectedIndex = 2;
                window.UpdateLayout();
                Check(window, "Advanced");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { window.Close(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal); }
        };
        timer.Start();
        Dispatcher.Run();
        return result;
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
        if (dark < 10000 || light < 100) throw new Exception($"{tab}: expected visible dark settings with text");
    }
}
