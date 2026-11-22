using System.Windows;
using System.Windows.Input;

namespace IvySay;
public static class AppCursors
{
    public static Cursor Arrow { get; } = Load("arrow.cur", Cursors.Arrow);
    public static Cursor Hand { get; } = Load("hand.cur", Cursors.Hand);
    private static Cursor Load(string file, Cursor fallback)
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/IvySay;component/{file}"));
            if (resource == null) return fallback;
            using (resource.Stream) return new Cursor(resource.Stream);
        }
        catch { return fallback; }
    }
}
