using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace IvySay;
// Original native interpretation of the interactive Kinetic Grid concept by satoriui.
// The referenced component source is not bundled; this draws independent WPF geometry.
public sealed class KineticGrid : FrameworkElement
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Window? host;
    private Point pointer = new(-1000, -1000), click = new(-1000, -1000);
    private double moved = -100, clicked = -100;
    private static readonly Brush Ink = MakeBrush(72, 92, 160, 136);
    private static Brush MakeBrush(byte a, byte r, byte g, byte b) { var v = new SolidColorBrush(Color.FromArgb(a, r, g, b)); v.Freeze(); return v; }
    public KineticGrid()
    {
        IsHitTestVisible = false;
        timer.Tick += (_, _) => { if (clock.Elapsed.TotalSeconds - Math.Max(moved, clicked) > 2.5) timer.Stop(); InvalidateVisual(); };
        Loaded += (_, _) => { host = Window.GetWindow(this); if (host != null) { host.PreviewMouseMove += Move; host.PreviewMouseDown += Down; } };
        Unloaded += (_, _) => { timer.Stop(); if (host != null) { host.PreviewMouseMove -= Move; host.PreviewMouseDown -= Down; host = null; } };
    }
    private void Move(object sender, MouseEventArgs e) { pointer = e.GetPosition(this); moved = clock.Elapsed.TotalSeconds; Wake(); }
    private void Down(object sender, MouseButtonEventArgs e) { click = e.GetPosition(this); clicked = clock.Elapsed.TotalSeconds; Wake(); }
    private void Wake() { if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast) timer.Start(); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double now = clock.Elapsed.TotalSeconds, age = now - clicked;
        bool motion = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        double fade = motion ? Math.Clamp(1 - (now - moved) / 2.5, 0, 1) : 0;
        double spacing = Math.Max(36, Math.Sqrt(Math.Max(1, ActualWidth * ActualHeight) / 1800));
        for (double y = spacing / 2; y < ActualHeight; y += spacing)
        for (double x = spacing / 2; x < ActualWidth; x += spacing)
        {
            double dx = x - pointer.X, dy = y - pointer.Y, dist = Math.Sqrt(dx * dx + dy * dy);
            double pull = fade * Math.Exp(-dist * dist / 26000) * 0.15;
            double px = x - dx * pull, py = y - dy * pull;
            if (motion && age >= 0 && age < 2.5)
            {
                double rx = x - click.X, ry = y - click.Y, r = Math.Max(1, Math.Sqrt(rx * rx + ry * ry));
                double ring = Math.Exp(-Math.Pow((r - age * 230) / 45, 2)) * Math.Exp(-age * 1.4) * 9;
                px += rx / r * ring; py += ry / r * ring;
            }
            dc.DrawEllipse(Ink, null, new Point(px, py), 1.5, 1.5);
        }
    }
}
