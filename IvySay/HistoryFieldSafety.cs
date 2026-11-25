using System.Windows.Automation;

namespace IvySay;
// Never read field text. Fail closed if the password state or focused identity is unavailable.
internal static class HistoryFieldSafety
{
    internal sealed record Target(IntPtr Window, uint ProcessId, IntPtr Control, int[] RuntimeId);
    private static int active;
    public static Task<T?> TryAsync<T>(Func<T?> work) where T : class
    {
        if (Interlocked.Exchange(ref active, 1) != 0) return Task.FromResult<T?>(null);
        return Task.Run(() => { try { return work(); } catch { return null; } finally { Interlocked.Exchange(ref active, 0); } });
    }
    public static Target? Capture((IntPtr Window, uint ProcessId) target, IntPtr control)
    {
        if (target.Window == IntPtr.Zero || target.ProcessId == 0 || control == IntPtr.Zero || Native.GetForegroundWindow() != target.Window || Native.FocusedControl(target.Window) != control) return null;
        var el = AutomationElement.FocusedElement;
        if (el == null || el.Current.ProcessId != target.ProcessId || el.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool password || password) return null;
        int[] id = el.GetRuntimeId();
        var again = AutomationElement.FocusedElement;
        if (again == null || again.Current.ProcessId != target.ProcessId || !again.GetRuntimeId().SequenceEqual(id) ||
            again.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is not bool afterPassword || afterPassword || Native.GetForegroundWindow() != target.Window || Native.FocusedControl(target.Window) != control) return null;
        return new Target(target.Window, target.ProcessId, control, id);
    }
    public static Target? Verify(Target expected)
    {
        var current = Capture((expected.Window, expected.ProcessId), expected.Control);
        return current != null && current.RuntimeId.SequenceEqual(expected.RuntimeId) ? current : null;
    }
}
