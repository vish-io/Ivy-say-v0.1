using System.Windows.Automation;

namespace IvySay;
internal static class SelectionReader
{
    // Only read an explicitly selected range, never the full field or clipboard.
    public static string? Read((IntPtr Window, uint ProcessId) target)
    {
        try
        {
            if (target.Window == IntPtr.Zero || target.ProcessId == 0 || Native.GetForegroundWindow() != target.Window) return null;
            var focused = AutomationElement.FocusedElement;
            if (focused == null || focused.Current.ProcessId != target.ProcessId ||
                focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool isPassword || isPassword ||
                !focused.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)) return null;
            var ranges = ((TextPattern)pattern).GetSelection();
            if (ranges.Length != 1) return null;
            string value = ranges[0].GetText(81).Trim();
            if (Native.GetForegroundWindow() != target.Window || value.Length is 0 or > 80 ||
                value.Any(c => char.IsControl(c) || c == ',')) return null;
            return value;
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException or NotSupportedException) { return null; }
    }
}
