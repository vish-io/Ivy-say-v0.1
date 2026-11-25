using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace IvySay;
internal static class NearbyContextReader
{
    // Called on a single background UI Automation worker; never waits on the recording path.
    internal sealed record Snapshot(string Text, int[] RuntimeId, IntPtr Window, uint ProcessId);
    public static string? Verify((IntPtr Window, uint ProcessId) target, IntPtr control, Snapshot first)
    {
        if (first.Window != target.Window || first.ProcessId != target.ProcessId) return null;
        var current = Read(target, control);
        return current != null && current.RuntimeId.SequenceEqual(first.RuntimeId) && current.Text == first.Text ? current.Text : null;
    }
    public static Snapshot? Read((IntPtr Window, uint ProcessId) target, IntPtr control)
    {
        try
        {
            if (target.Window == IntPtr.Zero || target.ProcessId == 0 || control == IntPtr.Zero || Native.GetForegroundWindow() != target.Window || Native.FocusedControl(target.Window) != control) return null;
            var el = AutomationElement.FocusedElement;
            if (el == null || el.Current.ProcessId != target.ProcessId ||
                el.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool password || password ||
                !el.TryGetCurrentPattern(TextPattern.Pattern, out object raw)) return null;
            int[] id = el.GetRuntimeId();
            var pattern = (TextPattern)raw;
            var selection = pattern.GetSelection();
            if (selection.Length != 1 || selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selection[0], TextPatternRangeEndpoint.End) != 0) return null;
            var caret = selection[0].Clone();
            var range = caret.Clone();
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -128);
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 128);
            string? text = NearbyContext.Validate(range.GetText(NearbyContext.MaxCharacters + 1));
            var after = AutomationElement.FocusedElement;
            if (text == null || Native.GetForegroundWindow() != target.Window || Native.FocusedControl(target.Window) != control || after == null || after.Current.ProcessId != target.ProcessId || !after.GetRuntimeId().SequenceEqual(id) ||
                after.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool afterPassword || afterPassword || !after.TryGetCurrentPattern(TextPattern.Pattern, out object lastPattern)) return null;
            var last = ((TextPattern)lastPattern).GetSelection();
            if (last.Length != 1 || last[0].CompareEndpoints(TextPatternRangeEndpoint.Start, last[0], TextPatternRangeEndpoint.End) != 0 || last[0].CompareEndpoints(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.Start) != 0) return null;
            return new Snapshot(text, id, target.Window, target.ProcessId);
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException or NotSupportedException) { return null; }
    }
}
