namespace IvySay;
internal static class PillVisibilityPolicy
{
    public static bool ShouldHide(bool enabled, bool hotkeyAvailable, bool recording, bool busy, bool settingsOpen, bool notificationAvailable) =>
        enabled && hotkeyAvailable && notificationAvailable && !recording && !busy && !settingsOpen;
}
