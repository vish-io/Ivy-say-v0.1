namespace IvySay;

// WPF controls share an HWND; use object identity to keep an in-app dictation
// attached to the exact editor that was focused at the start.
internal static class SettingsTargetGuard
{
    public static void Validate(object? original, object? current, bool visible, bool active)
    {
        if (original != null && (!visible || !active || !ReferenceEquals(original, current)))
            throw new InvalidOperationException("The original settings text box is no longer focused. Text was not fully inserted.");
    }
}
