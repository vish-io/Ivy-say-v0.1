namespace IvySay;
// Session-only progress; never store the observed text, window title or application name.
internal static class LearningFeedback
{
    private static string message = "No learning check has completed in this session.";
    private static bool pending;
    public static string Message => message;
    public static void Start() { pending = true; message = "Watching the recent insertion. Keep the same field focused and wait about 25 seconds before opening Settings."; }
    public static void Finish(string value) { pending = false; message = value; }
    public static void Cancel(string value) { if (pending) Finish(value); }
}
