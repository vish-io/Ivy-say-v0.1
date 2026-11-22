namespace IvySay;
internal static class PillVisibilityPolicy
{
    public static bool ShouldHide(bool enabled, bool hotkeyAvailable, bool recording, bool busy, bool settingsOpen) =>
        enabled && hotkeyAvailable && !recording && !busy && !settingsOpen;
}
