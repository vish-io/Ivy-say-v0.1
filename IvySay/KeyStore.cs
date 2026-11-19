using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IvySay;

// DPAPI binds the encrypted value to the current Windows account.
internal static class KeyStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IvySay");
    private static readonly string PathName = Path.Combine(DirectoryPath, "groq.key");
    public static bool Exists => File.Exists(PathName);
    public static void Save(string key)
    {
        byte[] plain = Encoding.UTF8.GetBytes(key);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(DirectoryPath);
            // Replace in one operation; the temporary file contains ciphertext only.
            string temp = PathName + ".tmp";
            try { File.WriteAllBytes(temp, encrypted); File.Move(temp, PathName, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static string Load()
    {
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(PathName), null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Delete() { if (File.Exists(PathName)) File.Delete(PathName); }
}
