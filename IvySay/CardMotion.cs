using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IvySay;

// Each visible card owns its own brush, so animation never changes shared resources.
public static class CardMotion
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CardMotion), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Border border) return;
        if ((bool)e.NewValue) { border.Loaded += Loaded; border.Unloaded += Unloaded; border.IsVisibleChanged += VisibilityChanged; if (border.IsLoaded) Start(border); }
        else { border.Loaded -= Loaded; border.Unloaded -= Unloaded; border.IsVisibleChanged -= VisibilityChanged; Stop(border); }
    }
    private static void Loaded(object sender, RoutedEventArgs e) => Start((Border)sender);
    private static void Unloaded(object sender, RoutedEventArgs e) => Stop((Border)sender);
    private static void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var border = (Border)sender;
        if (border.IsVisible) Start(border); else Stop(border);
    }
    private static void Start(Border border)
    {
        if (!border.IsLoaded || !border.IsVisible || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
        if (border.Background is not DrawingBrush brush) return;
        var copy = brush.Clone();
        if (copy.Drawing is not DrawingGroup group || group.Children.Count < 3 ||
            group.Children[1] is not GeometryDrawing { Brush: RadialGradientBrush glow } ||
            group.Children[2] is not GeometryDrawing { Brush: LinearGradientBrush bottom }) return;
        border.Background = copy;
        var move = new PointAnimation(new Point(0.72, 0.04), new Point(0.95, 0.15), TimeSpan.FromSeconds(7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        glow.BeginAnimation(RadialGradientBrush.CenterProperty, move);
        glow.BeginAnimation(RadialGradientBrush.GradientOriginProperty, move);
        bottom.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0.68, 1, TimeSpan.FromSeconds(4.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
    }
    private static void Stop(Border border)
    {
        if (border.Background is not DrawingBrush { Drawing: DrawingGroup group } || group.Children.Count < 3) return;
        if (group.Children[1] is GeometryDrawing { Brush: RadialGradientBrush glow } && !glow.IsFrozen) { glow.BeginAnimation(RadialGradientBrush.CenterProperty, null); glow.BeginAnimation(RadialGradientBrush.GradientOriginProperty, null); }
        if (group.Children[2] is GeometryDrawing { Brush: LinearGradientBrush bottom } && !bottom.IsFrozen) bottom.BeginAnimation(Brush.OpacityProperty, null);
    }
}
