// Composing/ribbon geometry adapted from thinking-orbs 0.3.2 by Jakub Antalik.
// MIT License; see docs/THINKING-ORBS-LICENSE.txt and https://orbs.jakubantalik.com.
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace IvySay;

// The supplied React component paints a 64px canvas then displays it at 56px.
// This is its monochrome composing preset ported to native WPF drawing primitives.
public sealed class ThinkingOrb : FrameworkElement
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long lastFrame;
    private bool subscribed;
    private readonly Dictionary<int, SolidColorBrush> brushes = new();
    private readonly record struct Dot(double X, double Y, double Z, double Radius, double White, double Alpha);
    public ThinkingOrb()
    {
        IsHitTestVisible = false;
        IsVisibleChanged += (_, _) => UpdateAnimation();
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => Unsubscribe();
    }
    private void UpdateAnimation()
    {
        if (IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation)
        {
            if (!subscribed) { CompositionTarget.Rendering += RenderFrame; subscribed = true; }
        }
        else Unsubscribe();
        InvalidateVisual();
    }
    private void Unsubscribe() { if (subscribed) { CompositionTarget.Rendering -= RenderFrame; subscribed = false; } }
    private void RenderFrame(object? sender, EventArgs e)
    {
        long now = clock.ElapsedMilliseconds;
        if (now - lastFrame < 33) return;
        lastFrame = now;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double t = SystemParameters.ClientAreaAnimation ? clock.Elapsed.TotalSeconds * 2.34 : 1.5;
        const double size = 64, radius = size / 2 * 0.78;
        double rs = Math.Pow(size / 300, 0.6);
        var dots = new List<Dot>(566);
        // The composing preset fixes spin at zero, with a camera tilt of 0.3.
        (double x, double y, double z) Project(double x, double y, double z) =>
            (32 + x, 32 - (y * Math.Cos(0.3) - z * Math.Sin(0.3)), y * Math.Sin(0.3) + z * Math.Cos(0.3));
        const int ghosts = 38;
        double golden = Math.PI * (3 - Math.Sqrt(5));
        for (int i = 0; i < ghosts; i++)
        {
            double y = 1 - 2 * (i + 0.5) / ghosts, r = Math.Sqrt(1 - y * y), a = i * golden;
            var p = Project(r * Math.Cos(a) * radius, y * radius, r * Math.Sin(a) * radius);
            double depth = (p.z / radius + 1) / 2;
            dots.Add(new(p.x, p.y, p.z, Math.Max(0.3, 0.8 * rs), 0.78, 0.1 + 0.22 * depth));
        }
        const int lanes = 12, segs = 44;
        double st = Math.Sin(0.55), ct = Math.Cos(0.55);
        for (int w = 0; w < lanes; w++)
        {
            double lane = (w - (lanes - 1) / 2.0) * 0.075;
            double edge = Math.Abs(w - (lanes - 1) / 2.0) / ((lanes - 1) / 2.0);
            for (int k = 0; k < segs; k++)
            {
                double a = k / (double)segs * 2 * Math.PI;
                double wob = 0.16 * Math.Sin(a * 3 - t * 1.7 + w * 0.22) + 0.07 * Math.Sin(a * 5 + t * 1.1);
                double off = lane + wob, x = Math.Cos(a), y = ct * Math.Sin(a) - st * off, z = st * Math.Sin(a) + ct * off;
                double length = Math.Sqrt(x * x + y * y + z * z);
                var p = Project(x / length * radius, y / length * radius, z / length * radius);
                double depth = (p.z / radius + 1) / 2;
                dots.Add(new(p.x, p.y, p.z, Math.Max(0.3, (0.935 + 1.445 * depth) * (1 - 0.25 * edge) * rs), 0.52 - 0.44 * depth + 0.18 * edge, 0.4 + 0.6 * depth));
            }
        }
        dots.Sort((a, b) => a.Z.CompareTo(b.Z));
        dc.PushTransform(new ScaleTransform(ActualWidth / size, ActualHeight / size));
        foreach (var dot in dots)
        {
            byte grey = (byte)Math.Clamp((int)Math.Round((1 - Math.Clamp(dot.White, 0, 1)) * 255), 0, 255);
            byte alpha = (byte)Math.Clamp((int)Math.Round(dot.Alpha * 255), 0, 255);
            // Quantize and reuse brushes to avoid hundreds of allocations per frame.
            grey = (byte)(grey / 8 * 8); alpha = (byte)(alpha / 8 * 8);
            int key = (alpha << 8) | grey;
            if (!brushes.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(Color.FromArgb(alpha, grey, grey, grey));
                brush.Freeze(); brushes.Add(key, brush);
            }
            dc.DrawEllipse(brush, null, new Point(dot.X, dot.Y), dot.Radius, dot.Radius);
        }
        dc.Pop();
    }
}
