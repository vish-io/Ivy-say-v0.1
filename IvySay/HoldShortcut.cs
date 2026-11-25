using System.Runtime.InteropServices;

namespace IvySay;

// Observe only the selected trigger and its modifiers; never record or log keystrokes.
// The selected trigger is consumed during an accepted hold; unrelated keys pass through.
internal sealed class HoldShortcut : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct KeyInfo { public uint Vk, Scan, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    private readonly ShortcutBinding binding;
    private readonly Action pressed, released;
    private readonly Func<bool> canStart;
    private readonly HookProc callback;
    private IntPtr hook;
    private bool down, suppressTrigger;
    // Hook events are authoritative for this consumed key, including after a modifier release.
    public bool TriggerHeld => suppressTrigger;
    public HoldShortcut(ShortcutBinding binding, Func<bool> canStart, Action pressed, Action released)
    {
        this.binding = binding; this.canStart = canStart; this.pressed = pressed; this.released = released;
        callback = OnKey;
        hook = SetWindowsHookEx(13, callback, IntPtr.Zero, 0);
        if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<KeyInfo>(data);
            if ((key.Flags & 0x10) == 0) // Ignore SendInput and other injected events.
            {
                int m = message.ToInt32(); bool isDown = m is 0x100 or 0x104, isUp = m is 0x101 or 0x105;
                if (key.Vk == binding.Key && suppressTrigger)
                {
                    if (isUp) { suppressTrigger = false; if (down) { down = false; released(); } }
                    if (isDown || isUp) return new IntPtr(1);
                }
                if (!down && isDown && key.Vk == binding.Key && ModifiersMatch() && canStart())
                {
                    down = true; suppressTrigger = true; pressed(); return new IntPtr(1);
                }
                if (down && isUp && IsRequiredModifier(key.Vk)) { down = false; released(); }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    private bool ModifiersMatch()
    {
        uint mods = (Native.KeyHeld(0x12) ? 1u : 0) | (Native.KeyHeld(0x11) ? 2u : 0) | (Native.KeyHeld(0x10) ? 4u : 0) | (Native.KeyHeld(0x5B) || Native.KeyHeld(0x5C) ? 8u : 0);
        return mods == binding.Modifiers;
    }
    private bool IsRequiredModifier(uint key) =>
        ((binding.Modifiers & 1) != 0 && key is 0x12 or 0xA4 or 0xA5) ||
        ((binding.Modifiers & 2) != 0 && key is 0x11 or 0xA2 or 0xA3) ||
        ((binding.Modifiers & 4) != 0 && key is 0x10 or 0xA0 or 0xA1) ||
        ((binding.Modifiers & 8) != 0 && key is 0x5B or 0x5C);
    public void Dispose() { if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
}
