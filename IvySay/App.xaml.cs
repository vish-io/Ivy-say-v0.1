using System.Windows;

namespace IvySay;
public partial class App : Application
{
    private System.Threading.Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new System.Threading.Mutex(true, @"Local\IvySay.SingleInstance", out bool created);
        if (!created) { IvyDialog.Show("Ivy Say is already running.", "Ivy Say"); Shutdown(); return; }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
