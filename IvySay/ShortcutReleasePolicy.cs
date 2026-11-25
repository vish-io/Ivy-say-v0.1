namespace IvySay;
internal static class ShortcutReleasePolicy
{
    // The hook consumes the trigger's key-up, so GetAsyncKeyState can be stale.
    // Modifier events are not consumed and must still be released before typing.
    public static bool IsHeld(bool holdMode, bool? hookTriggerHeld, bool osTriggerHeld, bool modifierHeld) =>
        (holdMode && hookTriggerHeld.HasValue ? hookTriggerHeld.Value : osTriggerHeld) || modifierHeld;
}
