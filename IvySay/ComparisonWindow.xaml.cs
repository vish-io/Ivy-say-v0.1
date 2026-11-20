using System.Windows;
namespace IvySay;
public partial class ComparisonWindow : Window
{
    public ComparisonWindow(string raw, string corrected, string? polished, string model, TimeSpan transcriptionTime, TimeSpan? formattingTime, string? error, bool polishEnabled, bool personalized)
    {
        InitializeComponent();
        RawText.Text = raw;
        PersonalText.Text = corrected;
        PersonalLabel.Text = personalized ? "2 · After your local corrections" : "2 · Local corrections disabled";
        if (polished != null)
        {
            OutputLabel.Text = "3 · AI formatter output (inserted)";
            OutputText.Text = polished;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · openai/gpt-oss-20b: {formattingTime!.Value.TotalSeconds:F2}s. The model may return text unchanged when no edit is needed.";
        }
        else
        {
            OutputLabel.Text = "3 · No AI output (stage 2 inserted)";
            OutputText.Text = corrected;
            Summary.Text = $"{model}: {transcriptionTime.TotalSeconds:F2}s · " + (polishEnabled ? $"Formatting failed: {error ?? "unknown error"}." : "AI formatting is disabled in settings.");
        }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
