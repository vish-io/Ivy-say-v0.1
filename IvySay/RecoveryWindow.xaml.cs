using System.Windows;
using System.Windows.Input;
namespace IvySay;
public partial class RecoveryWindow : Window
{
    private readonly bool allowUnsafeCopy;
    public RecoveryWindow(string text, string explanation, bool allowUnsafeCopy = false)
    {
        this.allowUnsafeCopy = allowUnsafeCopy;
        InitializeComponent();
        ResultText.Text = text;
        Explanation.Text = explanation + " Review or copy it below. This window does not save text. A recovered failure stays in memory until replaced or Ivy Say exits.";
    }
    private void CopyText(object sender, RoutedEventArgs e)
    {
        if (!allowUnsafeCopy && InsertionGuard.HasUnsafeCharacters(ResultText.Text)) { IvyDialog.Show("This text contains unsafe control characters. Remove them before copying.", "Ivy Say"); return; }
        if (string.IsNullOrEmpty(ResultText.Text)) return;
        try { Clipboard.SetText(ResultText.Text); }
        catch (System.Runtime.InteropServices.ExternalException) { IvyDialog.Show("The clipboard is busy. Please try again.", "Ivy Say"); }
    }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
