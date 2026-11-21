using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IvySay;

// Start animations on the actual template objects, not through WPF's template
// storyboard name scope (nested Freezables do not reliably register names there).
public static class ComboGlow
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ComboGlow), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ComboBox combo) return;
        if ((bool)e.NewValue) { combo.Loaded += Start; combo.Unloaded += Stop; if (combo.IsLoaded) Start(combo, new RoutedEventArgs()); }
        else { combo.Loaded -= Start; combo.Unloaded -= Stop; Stop(combo, new RoutedEventArgs()); }
    }
    private static void Start(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        foreach (string name in new[] { "Bloom", "Rim" })
        {
            if (combo.Template.FindName(name, combo) is not Border border || border.BorderBrush is not LinearGradientBrush brush) continue;
            var mutable = brush.Clone();
            var rotation = new RotateTransform(0, 0.5, 0.5);
            mutable.RelativeTransform = rotation;
            border.BorderBrush = mutable;
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3.2)) { RepeatBehavior = RepeatBehavior.Forever });
        }
    }
    private static void Stop(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.Template == null) return;
        foreach (string name in new[] { "Bloom", "Rim" })
            if (combo.Template.FindName(name, combo) is Border { BorderBrush: LinearGradientBrush { RelativeTransform: RotateTransform rotation } })
                rotation.BeginAnimation(RotateTransform.AngleProperty, null);
    }
}
