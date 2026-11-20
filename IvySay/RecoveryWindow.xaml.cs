using System.Windows;
namespace IvySay;
public partial class RecoveryWindow : Window
{
    public RecoveryWindow(string text, string explanation)
    {
        InitializeComponent();
        ResultText.Text = text;
        Explanation.Text = explanation + " Review or copy it below. This text is not saved by Ivy Say and will be lost when you close this window.";
    }
    private void CopyText(object sender, RoutedEventArgs e)
    {
        if (InsertionGuard.HasUnsafeCharacters(ResultText.Text)) { MessageBox.Show("This text contains unsafe control characters. Remove them before copying.", "Ivy Say"); return; }
        if (string.IsNullOrEmpty(ResultText.Text)) return;
        try { Clipboard.SetText(ResultText.Text); }
        catch (System.Runtime.InteropServices.ExternalException) { MessageBox.Show("The clipboard is busy. Please try again.", "Ivy Say"); }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
