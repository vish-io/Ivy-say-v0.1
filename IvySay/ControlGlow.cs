using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace IvySay;
public static class ControlGlow
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ControlGlow), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
        if (obj is not Control c) return;
        if ((bool)e.NewValue) { c.Loaded += Start; c.Unloaded += Stop; if (c.IsLoaded) Start(c, new RoutedEventArgs()); }
        else { c.Loaded -= Start; c.Unloaded -= Stop; Stop(c, new RoutedEventArgs()); }
    }
    private static void Start(object sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Template?.FindName("Shine", c) is not Border b || b.BorderBrush is not LinearGradientBrush brush) return;
        var copy = brush.Clone(); var rotation = new RotateTransform(0, 0.5, 0.5); copy.RelativeTransform = rotation; b.BorderBrush = copy;
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(5)) { RepeatBehavior = RepeatBehavior.Forever });
    }
    private static void Stop(object sender, RoutedEventArgs e)
    {
        if (sender is Control c && c.Template?.FindName("Shine", c) is Border { BorderBrush: LinearGradientBrush { RelativeTransform: RotateTransform r } }) r.BeginAnimation(RotateTransform.AngleProperty, null);
    }
}
