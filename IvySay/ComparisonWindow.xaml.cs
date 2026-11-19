using System.Windows;
namespace IvySay;
public partial class ComparisonWindow : Window
{
    public ComparisonWindow(string raw, string? polished, string model, TimeSpan transcriptionTime, TimeSpan? formattingTime, string? error, bool polishEnabled)
    {
        InitializeComponent();
        RawText.Text = raw;
        if (polished != null)
        {
            OutputLabel.Text = "2 · AI formatter output (inserted)";
            OutputText.Text = polished;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · openai/gpt-oss-20b: {formattingTime!.Value.TotalSeconds:F2}s. The model may return text unchanged when no edit is needed.";
        }
        else
        {
            OutputLabel.Text = "2 · No AI output (raw transcript inserted)";
            OutputText.Text = raw;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · " + (polishEnabled ? $"Formatting failed: {error ?? "unknown error"}." : "AI formatting is disabled in settings.");
        }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
