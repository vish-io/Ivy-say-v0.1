// From Uiverse.io by andrew-manzyk - https://uiverse.io/andrew-manzyk/young-walrus-64
// MIT License, Copyright (c) andrew-manzyk (Andrew Manzyk)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace IvySay;

// Native WPF adaptation of the 100px CSS/SVG orb, scaled to the existing pill.
// WPF has no CSS contrast/hue-rotate filters, so animated blur and colors approximate them.
public sealed class LoaderOrb : UserControl
{
    private readonly List<(RotateTransform Transform, double Start, double Direction)> rotations = new();
    private readonly List<(GradientStop Stop, byte Alpha, bool Light)> gradientStops = new();
    private readonly List<(DropShadowEffect Effect, bool Light)> shadows = new();
    private readonly BlurEffect maskBlur = new() { Radius = 7, RenderingBias = RenderingBias.Performance };
    private static readonly string[] LightColors = { "#ffbf48", "#ff6448", "#ff4888", "#ff48e4", "#ff485a", "#ffbf48" };
    private static readonly string[] DarkColors = { "#be4a1d", "#be1d41", "#be1d91", "#9a1dbe", "#be1d69", "#be4a1d" };

    public LoaderOrb()
    {
        Width = 40;
        Height = 40;
        Focusable = false;
        IsHitTestVisible = false;
        var orb = new Grid { Width = 100, Height = 100, ClipToBounds = false };

        // Soft outer glow and circular gradient correspond to .loader and ::before.
        var glow = new Ellipse { Fill = Gradient((0, 53, true), (1, 53, true)) };
        var glowEffect = new DropShadowEffect { Color = ColorFrom("#ffbf48", 255), BlurRadius = 25, ShadowDepth = 0, Opacity = 0.5, RenderingBias = RenderingBias.Performance };
        glow.Effect = glowEffect;
        shadows.Add((glowEffect, true));
        orb.Children.Add(glow);

        var disc = new Ellipse { Fill = Gradient((0, 64, true), (1, 128, false)), Stroke = Gradient((0, 255, true), (1, 255, false)), StrokeThickness = 1 };
        orb.Children.Add(disc);

        // White triangles are a soft opacity mask over the orange gradient.
        var mask = new Canvas { Width = 100, Height = 100, Background = Brushes.Transparent, Effect = maskBlur };
        AddTriangle(mask, new Point(25, 25), new Point(75, 25), new Point(50, 75), 50, 50, 0, -1);
        AddTriangle(mask, new Point(50, 25), new Point(75, 75), new Point(25, 75), 50, 60, 120, 1);
        AddTriangle(mask, new Point(35, 35), new Point(65, 35), new Point(50, 65), 40, 40, 0, -1);
        AddTriangle(mask, new Point(35, 35), new Point(65, 35), new Point(50, 65), 40, 40, -180, -1);
        AddTriangle(mask, new Point(35, 35), new Point(65, 35), new Point(50, 65), 60, 40, 0, 1);
        AddTriangle(mask, new Point(35, 35), new Point(65, 35), new Point(50, 65), 60, 40, 240, 1);
        // VisualBrush visuals are not in the visible tree, so arrange their coordinate space explicitly.
        mask.Measure(new Size(100, 100));
        mask.Arrange(new Rect(0, 0, 100, 100));
        var shape = new Rectangle
        {
            Width = 100, Height = 100,
            Fill = Gradient((0.3, 255, true), (0.7, 255, false)),
            OpacityMask = new VisualBrush(mask) { Stretch = Stretch.Fill }
        };
        orb.Children.Add(shape);
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = orb };
        Loaded += (_, _) => StartAnimation();
        Unloaded += (_, _) => StopAnimation();
    }

    private void AddTriangle(Canvas canvas, Point a, Point b, Point c, double centerX, double centerY, double start, double direction)
    {
        var rotation = new RotateTransform(start, centerX, centerY);
        var polygon = new Polygon { Points = new PointCollection { a, b, c }, Fill = Brushes.White, RenderTransform = rotation };
        canvas.Children.Add(polygon);
        rotations.Add((rotation, start, direction));
    }

    private LinearGradientBrush Gradient(params (double Offset, byte Alpha, bool Light)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        foreach (var (offset, alpha, light) in stops)
        {
            var stop = new GradientStop(ColorFrom(light ? LightColors[0] : DarkColors[0], alpha), offset);
            brush.GradientStops.Add(stop);
            gradientStops.Add((stop, alpha, light));
        }
        return brush;
    }

    private static Color ColorFrom(string color, byte alpha)
    {
        var value = (Color)ColorConverter.ConvertFromString(color);
        value.A = alpha;
        return value;
    }

    private void StartAnimation()
    {
        foreach (var (rotation, start, direction) in rotations)
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(start, start + 360 * direction, TimeSpan.FromSeconds(2)) { RepeatBehavior = RepeatBehavior.Forever });
        foreach (var (stop, alpha, light) in gradientStops)
            stop.BeginAnimation(GradientStop.ColorProperty, ColorCycle(light, alpha));
        foreach (var (effect, light) in shadows)
            effect.BeginAnimation(DropShadowEffect.ColorProperty, ColorCycle(light, 255));

        // CSS animates the contrast of a blurred mask; WPF approximates it with a soft pulse.
        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(1), RepeatBehavior = RepeatBehavior.Forever };
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(7, KeyTime.FromPercent(0)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(3, KeyTime.FromPercent(0.2)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(3, KeyTime.FromPercent(0.4)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(7, KeyTime.FromPercent(0.6)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(7, KeyTime.FromPercent(1)));
        maskBlur.BeginAnimation(BlurEffect.RadiusProperty, pulse);
    }

    private static ColorAnimationUsingKeyFrames ColorCycle(bool light, byte alpha)
    {
        var animation = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(6), RepeatBehavior = RepeatBehavior.Forever };
        string[] colors = light ? LightColors : DarkColors;
        for (int i = 0; i < colors.Length; i++)
            animation.KeyFrames.Add(new EasingColorKeyFrame(ColorFrom(colors[i], alpha), KeyTime.FromPercent(i / 5d), new SineEase { EasingMode = EasingMode.EaseInOut }));
        return animation;
    }

    private void StopAnimation()
    {
        foreach (var (rotation, _, _) in rotations) rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        foreach (var (stop, _, _) in gradientStops) stop.BeginAnimation(GradientStop.ColorProperty, null);
        foreach (var (effect, _) in shadows) effect.BeginAnimation(DropShadowEffect.ColorProperty, null);
        maskBlur.BeginAnimation(BlurEffect.RadiusProperty, null);
    }
}
