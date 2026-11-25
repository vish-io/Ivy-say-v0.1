using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace IvySay;
internal sealed class PersonalizationData
{
    public string Vocabulary { get; set; } = "";
    public string LearnedWords { get; set; } = "";
    public string Corrections { get; set; } = "";
    public string Snippets { get; set; } = "";
}
internal static class PersonalizationStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "personalization.dat");
    private static PersonalizationData? cached;
    private static PersonalizationData Copy(PersonalizationData data) => new() { Vocabulary = data.Vocabulary, LearnedWords = data.LearnedWords, Corrections = data.Corrections, Snippets = data.Snippets };
    public static PersonalizationData Load()
    {
        if (cached != null) return Copy(cached);
        if (!File.Exists(FilePath)) return Copy(cached = new());
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
        try
        {
            var data = JsonSerializer.Deserialize<PersonalizationData>(plain) ?? throw new InvalidDataException();
            if (data.Vocabulary is null || data.LearnedWords is null || data.LearnedWords.Length > 1000 || data.Corrections is null || data.Vocabulary.Length > 1000 || data.Corrections.Length > 4500 || data.Snippets is null || data.Snippets.Length > 10000) throw new InvalidDataException();
            PersonalizationRules.Parse(data.Corrections);
            SnippetRules.Parse(data.Snippets);
            cached = Copy(data);
            return Copy(data);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Save(PersonalizationData data)
    {
        if (data.Vocabulary.Length > 1000 || data.LearnedWords.Length > 1000 || data.Corrections.Length > 4500 || data.Snippets.Length > 10000) throw new ArgumentException("Personalization is too long.");
        if (data.Vocabulary.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw new ArgumentException("Vocabulary contains unsupported characters.");
        if (data.LearnedWords.Any(c => char.IsControl(c) && c is not ('\r' or '\n'))) throw new ArgumentException("Learned words contain unsupported characters.");
        PersonalizationRules.Parse(data.Corrections);
        SnippetRules.Parse(data.Snippets);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(data);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + ".tmp";
            try { File.WriteAllBytes(temp, encrypted); File.Move(temp, FilePath, true); cached = Copy(data); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Delete() { if (File.Exists(FilePath)) File.Delete(FilePath); cached = null; }
}
