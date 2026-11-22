namespace IvySay;

internal static class AppContextData
{
    // Process names are untrusted data. Reject unusual names rather than sending paths,
    // control characters, markup, or arbitrary user-controlled instructions to the model.
    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80) return null;
        foreach (char c in name)
            if (!(char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-' or '.')) return null;
        return name;
    }
}
