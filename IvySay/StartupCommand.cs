using System.IO;

namespace IvySay;
internal static class StartupCommand
{
    public const string Argument = "--startup";
    // A development run may be hosted by dotnet.exe; never register it as the app.
    public static string? ForExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0 ||
            !string.Equals(Path.GetFileName(path), "IvySay.exe", StringComparison.OrdinalIgnoreCase)) return null;
        return "\"" + path + "\" " + Argument;
    }
}
