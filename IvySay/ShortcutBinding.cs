namespace IvySay;

// Windows virtual key and RegisterHotKey modifier flags. Legacy options map here unchanged.
internal readonly record struct ShortcutBinding(int Key, uint Modifiers, bool Hold)
{
    public bool Valid => Key is >= 1 and <= 254 && (Modifiers & ~15u) == 0 && !(Key is >= 0x10 and <= 0x12 or >= 0x5B and <= 0x5C or >= 0xA0 and <= 0xA5);
    public static ShortcutBinding From(Preferences p) => p.ShortcutKey > 0
        ? new(p.ShortcutKey, p.ShortcutModifiers, p.ShortcutMode == "Hold")
        : p.Shortcut switch { "Oem3" => new(0xC0, 0, false), "CtrlShiftSpace" => new(0x20, 6, false), "AltShiftSpace" => new(0x20, 5, false), _ => new(0x20, 10, false) };
}
