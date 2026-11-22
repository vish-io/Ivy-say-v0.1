using System.Runtime.InteropServices;
using System.Diagnostics;

namespace IvySay;
internal static class Native
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000, VK_SPACE = 0x20;
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    public static bool KeyHeld(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    public static bool ShortcutHeld(ShortcutBinding shortcut) => KeyHeld(shortcut.Key) || KeyHeld(0x11) || KeyHeld(0x10) || KeyHeld(0x12) || KeyHeld(0x5B) || KeyHeld(0x5C);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct GUITHREADINFO
    {
        public uint size, flags;
        public IntPtr active, focus, capture, menuOwner, moveSize, caret;
        public RECT caretRect;
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    public static (IntPtr Window, uint ProcessId) CaptureForeground()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out uint pid) == 0) return (window, 0);
        return (window, pid);
    }
    public static string? ResolveProcessName((IntPtr Window, uint ProcessId) snapshot)
    {
        if (snapshot.Window == IntPtr.Zero || snapshot.ProcessId == 0) return null;
        try
        {
            using var process = Process.GetProcessById(checked((int)snapshot.ProcessId));
            string name = process.ProcessName;
            if (process.HasExited || GetWindowThreadProcessId(snapshot.Window, out uint current) == 0 || current != snapshot.ProcessId) return null;
            return AppContextData.Normalize(name);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException or NotSupportedException) { return null; }
    }
    public static IntPtr FocusedControl(IntPtr window)
    {
        uint thread = GetWindowThreadProcessId(window, out _);
        if (thread == 0) return IntPtr.Zero;
        var info = new GUITHREADINFO { size = (uint)Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(thread, ref info) ? info.focus : IntPtr.Zero;
    }
    public static (bool risky, bool unknown) AssessTarget(IntPtr window)
    {
        if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out uint pid) == 0 || pid == 0) return (false, true);
        try
        {
            using var process = Process.GetProcessById(checked((int)pid));
            string name = process.ProcessName;
            string[] consoles = { "WindowsTerminal", "WindowsTerminalPreview", "OpenConsole", "conhost", "cmd", "powershell", "pwsh", "wsl", "bash", "mintty", "putty", "wezterm-gui", "alacritty", "kitty", "ConEmu64", "ConEmu", "wt" };
            return (consoles.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)), false);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException or NotSupportedException) { return (false, true); }
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
    public static void TypeText(string text, IntPtr target, IntPtr focus, Action<char>? onAccepted = null, Action? validateTarget = null)
    {
        validateTarget?.Invoke();
        if (target == IntPtr.Zero || GetForegroundWindow() != target) throw new InvalidOperationException("The target window changed. Text was not inserted; try again in the intended app.");
        if (focus != IntPtr.Zero && FocusedControl(target) != focus) throw new InvalidOperationException("The focused control changed.");
        // Unicode input avoids clobbering the user's clipboard. Recheck focus for each character.
        foreach (char c in text)
        {
            validateTarget?.Invoke();
            if (GetForegroundWindow() != target || (focus != IntPtr.Zero && FocusedControl(target) != focus)) throw new InvalidOperationException("Focus changed during insertion. Some text may have been inserted.");
            var inputs = new INPUT[2];
            inputs[0].type = inputs[1].type = 1;
            inputs[0].data.keyboard = new KEYBDINPUT { scan = c, flags = 4 };
            inputs[1].data.keyboard = new KEYBDINPUT { scan = c, flags = 4 | 2 };
            if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new InvalidOperationException("Windows blocked text insertion. Try an app running at the same privilege level.");
            onAccepted?.Invoke(c);
        }
    }
    // Completed output: submit all Unicode key events together, without using the clipboard.
    // Focus checks are best effort; focus can still change after events are queued.
    public static void TypeTextBatch(string text, IntPtr target, IntPtr focus, Action<char>? onAccepted = null, Action? validateTarget = null)
    {
        BatchInsertion.Send(text, () =>
        {
            validateTarget?.Invoke();
            if (target == IntPtr.Zero || GetForegroundWindow() != target || (focus != IntPtr.Zero && FocusedControl(target) != focus))
                throw new InvalidOperationException("The target or focused control changed. Text was not inserted.");
        }, value =>
        {
            var inputs = new INPUT[checked(value.Length * 2)];
            for (int i = 0; i < value.Length; i++)
            {
                inputs[i * 2].type = inputs[i * 2 + 1].type = 1;
                inputs[i * 2].data.keyboard = new KEYBDINPUT { scan = value[i], flags = 4 };
                inputs[i * 2 + 1].data.keyboard = new KEYBDINPUT { scan = value[i], flags = 4 | 2 };
            }
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }, onAccepted);
    }

}
