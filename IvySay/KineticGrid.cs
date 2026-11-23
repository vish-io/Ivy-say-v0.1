using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace IvySay;

// Native WPF adaptation of the Kinetic Grid component supplied by the user
// (reference: 21st.dev/@satoriui/components/kinetic-grid). No web runtime required.
public sealed class KineticGrid : FrameworkElement
{
    private const double CellSize = 55, InfluenceRadius = 260, MaxWarp = 24, DotSpacing = 28;
    private const int Levels = 64, MaxRipples = 8;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<(Point Origin, double Born)> ripples = new();
    private Window? host;
    private Point mouse = new(-9999, -9999), target = new(-9999, -9999);
    private bool inside;
    private static readonly Brush BackgroundInk = FrozenBrush(Color.FromRgb(22, 22, 24));
    private static readonly Brush TextureInk = FrozenBrush(Color.FromArgb(13, 255, 255, 255));
    private static readonly Pen[] LinePens = Enumerable.Range(0, Levels + 1).Select(i => LinePen(i / (double)Levels)).ToArray();
    private static readonly Brush[] NodeBrushes = Enumerable.Range(0, Levels + 1).Select(i => FrozenBrush(Blend(Color.FromArgb(51, 255, 255, 255), Color.FromArgb(255, 74, 158, 255), i / (double)Levels))).ToArray();
    private static Brush FrozenBrush(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Clamp((int)Math.Round(a.A + (b.A - a.A) * t), 0, 255),
        (byte)Math.Clamp((int)Math.Round(a.R + (b.R - a.R) * t), 0, 255),
        (byte)Math.Clamp((int)Math.Round(a.G + (b.G - a.G) * t), 0, 255),
        (byte)Math.Clamp((int)Math.Round(a.B + (b.B - a.B) * t), 0, 255));
    private static Pen LinePen(double t)
    {
        var pen = new Pen(FrozenBrush(Blend(Color.FromArgb(33, 255, 255, 255), Color.FromArgb(230, 74, 158, 255), t)), 0.8 + 0.7 * t);
        pen.Freeze(); return pen;
    }
    private static int Level(double t) => Math.Clamp((int)Math.Round(t * Levels), 0, Levels);
    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static bool MotionEnabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public KineticGrid()
    {
        IsHitTestVisible = false;
        timer.Tick += Tick;
        Loaded += Attach;
        Unloaded += Detach;
        IsVisibleChanged += (_, _) => { if (!IsVisible) timer.Stop(); else InvalidateVisual(); };
    }
    private void Attach(object sender, RoutedEventArgs e)
    {
        host = Window.GetWindow(this);
        if (host == null) return;
        host.PreviewMouseMove += Move;
        host.PreviewMouseDown += Down;
        host.MouseLeave += Leave;
    }
    private void Detach(object sender, RoutedEventArgs e)
    {
        timer.Stop(); ripples.Clear(); inside = false;
        if (host != null) { host.PreviewMouseMove -= Move; host.PreviewMouseDown -= Down; host.MouseLeave -= Leave; host = null; }
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!MotionEnabled) return;
        target = e.GetPosition(this);
        if (!inside) { mouse = target; inside = true; }
        Wake();
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        if (!MotionEnabled || e.ChangedButton != MouseButton.Left) return;
        Point p = e.GetPosition(this);
        if (ripples.Count >= MaxRipples) ripples.RemoveAt(0);
        ripples.Add((p, clock.Elapsed.TotalSeconds));
        Wake();
    }
    private void Leave(object sender, MouseEventArgs e)
    {
        inside = false; mouse = target = new Point(-9999, -9999);
        if (ripples.Count == 0) timer.Stop();
        InvalidateVisual();
    }
    private void Wake() { if (IsVisible && MotionEnabled && !timer.IsEnabled) timer.Start(); InvalidateVisual(); }
    private void Tick(object? sender, EventArgs e)
    {
        if (!IsVisible || !MotionEnabled) { timer.Stop(); ripples.Clear(); InvalidateVisual(); return; }
        // 0.08 interpolation at 60 Hz as in the supplied reference.
        mouse = new Point(mouse.X + (target.X - mouse.X) * 0.08, mouse.Y + (target.Y - mouse.Y) * 0.08);
        double now = clock.Elapsed.TotalSeconds;
        ripples.RemoveAll(r => now - r.Born >= 1 / 1.2);
        if ((mouse - target).Length < 0.15 && ripples.Count == 0) { mouse = target; timer.Stop(); }
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        dc.DrawRectangle(BackgroundInk, null, new Rect(0, 0, width, height));
        for (double x = DotSpacing / 2; x < width; x += DotSpacing)
            for (double y = DotSpacing / 2; y < height; y += DotSpacing)
                dc.DrawEllipse(TextureInk, null, new Point(x, y), 0.9, 0.9);

        // Bound work for unusually large desktop surfaces; normal window sizes use 55px cells.
        double cell = Math.Max(CellSize, Math.Sqrt(width * height / 2400));
        int cols = Math.Max(2, (int)Math.Ceiling(width / cell)) + 1;
        int rows = Math.Max(2, (int)Math.Ceiling(height / cell)) + 1;
        var points = new Point[rows, cols];
        var proximity = new double[rows, cols];
        double now = clock.Elapsed.TotalSeconds;
        bool motion = MotionEnabled;
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            double x = col * width / (cols - 1), y = row * height / (rows - 1);
            double colPin = Math.Min(1, Math.Min(col / 1.5, (cols - 1 - col) / 1.5));
            double rowPin = Math.Min(1, Math.Min(row / 1.5, (rows - 1 - row) / 1.5));
            double pin = colPin * colPin * rowPin * rowPin;
            double dx = x - mouse.X, dy = y - mouse.Y, dist = Math.Sqrt(dx * dx + dy * dy);
            double pr = motion && inside ? Math.Max(0, 1 - dist / InfluenceRadius) * pin : 0;
            double px = x, py = y;
            if (pr > 0 && dist > 0)
            {
                double t = dist / InfluenceRadius;
                double eased = t < 0.01 ? 0 : (1 - t) * (1 - t) * Math.Min(1, dist / 60);
                double warp = eased * MaxWarp * pin;
                px -= dx / dist * warp; py -= dy / dist * warp;
            }
            if (motion)
                foreach (var ripple in ripples)
                {
                    double age = Math.Max(0, now - ripple.Born), radius = age * 400, opacity = Math.Max(0, 1 - age * 1.2);
                    double rx = x - ripple.Origin.X, ry = y - ripple.Origin.Y, rd = Math.Sqrt(rx * rx + ry * ry), diff = rd - radius;
                    if (rd > 0 && Math.Abs(diff) < 55)
                    {
                        double strength = (1 - Math.Abs(diff) / 55) * opacity * 18 * pin * (diff < 0 ? 1 : -1);
                        px += rx / rd * strength; py += ry / rd * strength;
                    }
                }
            points[row, col] = new Point(px, py); proximity[row, col] = pr;
        }
        void Segment(int r1, int c1, int r2, int c2)
        {
            double amount = Smooth((proximity[r1, c1] + proximity[r2, c2]) / 2);
            dc.DrawLine(LinePens[Level(amount)], points[r1, c1], points[r2, c2]);
        }
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols - 1; c++) Segment(r, c, r, c + 1);
        for (int c = 0; c < cols; c++) for (int r = 0; r < rows - 1; r++) Segment(r, c, r + 1, c);
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
        {
            double amount = Smooth(proximity[r, c]), radius = 1.8 + 1.4 * amount;
            Point p = points[r, c];
            if (amount > 0.3)
            {
                double glowRadius = radius + 6 * (amount - 0.3) / 0.7;
                var glow = new RadialGradientBrush(Color.FromArgb((byte)(amount * 76), 74, 158, 255), Color.FromArgb(0, 74, 158, 255));
                dc.DrawEllipse(glow, null, p, glowRadius, glowRadius);
            }
            dc.DrawEllipse(NodeBrushes[Level(amount)], null, p, radius, radius);
        }
        if (motion)
            foreach (var ripple in ripples)
            {
                double age = Math.Max(0, now - ripple.Born), opacity = Math.Max(0, 1 - age * 1.2);
                var pen = new Pen(FrozenBrush(Color.FromArgb((byte)(opacity * 71), 100, 180, 255)), 1.5);
                dc.DrawEllipse(null, pen, ripple.Origin, age * 400, age * 400);
            }
    }
}
