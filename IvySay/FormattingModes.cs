namespace IvySay;
internal static class FormattingModes
{
    public static string Normalize(string? mode) => mode is "concise" or "structured" ? mode : "clean";
    public static string Next(string? mode) => Normalize(mode) switch { "clean" => "concise", "concise" => "structured", _ => "clean" };
    public static string Label(string? mode) => Normalize(mode) switch { "concise" => "Concise", "structured" => "Structured", _ => "Clean" };
}
