namespace IvySay;

// These checks are defense in depth, not a guarantee that the target is safe.
internal static class InsertionGuard
{
    public static bool HasUnsafeCharacters(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((char.IsControl(c) && c is not ('\r' or '\n' or '\t')) || (c >= '\u202a' && c <= '\u202e') || (c >= '\u2066' && c <= '\u2069')) return true;
            if (char.IsHighSurrogate(c)) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return true; }
            else if (char.IsLowSurrogate(c)) return true;
        }
        return false;
    }
    public static string? ReviewReason(string text, bool riskyTarget, bool unknownTarget, string? securityLevel)
    {
        if (securityLevel == "Unrestricted") return null;
        if (HasUnsafeCharacters(text)) return "The result contains unsafe control characters. Remove them before copying.";
        if (securityLevel == "ReviewAll") return "Review-before-insert is enabled. Nothing was typed automatically.";
        if (unknownTarget) return "The target or focused control could not be verified. Nothing was typed automatically.";
        if (riskyTarget) return "A terminal or console was detected. Commands can run when pasted, so nothing was typed automatically.";
        if (text.Contains('\n') || text.Contains('\r') || text.Contains('\t')) return "The result contains line breaks or tabs, which can trigger actions in some apps. Nothing was typed automatically.";
        return null;
    }
}
