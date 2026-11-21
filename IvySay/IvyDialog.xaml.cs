using System.Windows;

namespace IvySay;

// Modal, conservative confirmations: closing, Escape and Enter on a Yes/No
// prompt all choose No unless the user explicitly chooses Yes.
public partial class IvyDialog : Window
{
    private MessageBoxResult result;
    private IvyDialog(string text, string caption, MessageBoxButton buttons, MessageBoxImage image)
    {
        InitializeComponent();
        Heading.Text = caption;
        Title = caption;
        Message.Text = text;
        bool confirm = buttons == MessageBoxButton.YesNo;
        result = confirm ? MessageBoxResult.No : MessageBoxResult.OK;
        NoButton.Visibility = YesButton.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Visibility = confirm ? Visibility.Collapsed : Visibility.Visible;
        NoButton.IsDefault = NoButton.IsCancel = confirm;
        OkButton.IsDefault = OkButton.IsCancel = !confirm;
        if (image == MessageBoxImage.Warning || image == MessageBoxImage.Error)
        {
            Severity.Text = image == MessageBoxImage.Error ? "Please check" : "Important";
            Severity.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => { if (confirm) NoButton.Focus(); else OkButton.Focus(); };
    }
    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
    {
        if (buttons != MessageBoxButton.OK && buttons != MessageBoxButton.YesNo) throw new ArgumentOutOfRangeException(nameof(buttons));
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.IsActive)
                    ?? (Application.Current?.MainWindow is { IsVisible: true } main ? main : null);
        var dialog = new IvyDialog(message, caption, buttons, image);
        if (owner != null) { dialog.Owner = owner; dialog.Topmost = owner.Topmost; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog.result;
    }
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
    private void NoClicked(object sender, RoutedEventArgs e) { result = MessageBoxResult.No; Close(); }
    private void YesClicked(object sender, RoutedEventArgs e) { result = MessageBoxResult.Yes; Close(); }
    private void OkClicked(object sender, RoutedEventArgs e) { result = MessageBoxResult.OK; Close(); }
}
