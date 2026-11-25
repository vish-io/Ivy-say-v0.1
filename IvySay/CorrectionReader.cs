using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace IvySay;
// Runs off the UI thread. Never reads a password or a field with unknown password state.
internal static class CorrectionReader
{
    internal sealed record Snapshot(int[] RuntimeId, string Before, string After, bool AtStart, bool AtEnd);
    private static int active;
    public static Task<T?> TryAsync<T>(Func<T?> action) where T : class
    {
        if (Interlocked.Exchange(ref active, 1) != 0) return Task.FromResult<T?>(null);
        return Task.Run(() => { try { return action(); } catch { return null; } finally { Interlocked.Exchange(ref active, 0); } });
    }
    private static (AutomationElement Element, TextPattern Pattern)? Focused(IntPtr window, uint pid)
    {
        if (window == IntPtr.Zero || pid == 0 || Native.GetForegroundWindow() != window) return null;
        var el = AutomationElement.FocusedElement;
        if (el == null || el.Current.ProcessId != pid ||
            el.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool password || password ||
            !el.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)) return null;
        return (el, (TextPattern)pattern);
    }
    public static Snapshot? Before(IntPtr window, uint pid)
    {
        var focus = Focused(window, pid); if (focus == null) return null;
        var (el, pattern) = focus.Value;
        var selected = pattern.GetSelection();
        if (selected.Length != 1 || selected[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selected[0], TextPatternRangeEndpoint.End) != 0) return null;
        var left = selected[0].Clone(); var right = selected[0].Clone();
        left.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -24);
        right.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 24);
        var doc = pattern.DocumentRange;
        bool atStart = left.CompareEndpoints(TextPatternRangeEndpoint.Start, doc, TextPatternRangeEndpoint.Start) == 0;
        bool atEnd = right.CompareEndpoints(TextPatternRangeEndpoint.End, doc, TextPatternRangeEndpoint.End) == 0;
        string before = left.GetText(25), after = right.GetText(25);
        int[] id = el.GetRuntimeId();
        if ((!atStart && before.Length < 12) || (!atEnd && after.Length < 12) || before.Length > 24 || after.Length > 24 || !StillFocused(window, pid, id)) return null;
        return new Snapshot(id, before, after, atStart, atEnd);
    }
    public static string? After(IntPtr window, uint pid, Snapshot baseline)
    {
        var focus = Focused(window, pid); if (focus == null) return null;
        var (el, pattern) = focus.Value;
        if (!el.GetRuntimeId().SequenceEqual(baseline.RuntimeId)) return null;
        var selected = pattern.GetSelection();
        if (selected.Length != 1 || selected[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selected[0], TextPatternRangeEndpoint.End) != 0) return null;
        var range = selected[0].Clone();
        range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -256);
        range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 256);
        var doc = pattern.DocumentRange;
        bool reachesStart = range.CompareEndpoints(TextPatternRangeEndpoint.Start, doc, TextPatternRangeEndpoint.Start) == 0;
        bool reachesEnd = range.CompareEndpoints(TextPatternRangeEndpoint.End, doc, TextPatternRangeEndpoint.End) == 0;
        string text = range.GetText(513);
        if (text.Length > 512 || !StillFocused(window, pid, baseline.RuntimeId)) return null;
        return CorrectionLearning.Extract(text, baseline.Before, baseline.After, baseline.AtStart, baseline.AtEnd, reachesStart, reachesEnd);
    }
    private static bool StillFocused(IntPtr window, uint pid, int[] id)
    {
        var focus = Focused(window, pid);
        return focus != null && focus.Value.Element.GetRuntimeId().SequenceEqual(id) && Native.GetForegroundWindow() == window;
    }
}
