using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IntentShell.Services;

public static class ApiKeyStore
{
    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyCommand");

        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "apikey.dat");
    }

    public static string? Load()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path)) return null;

            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var key = Encoding.UTF8.GetString(bytes).Trim();

            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is empty.", nameof(apiKey));

        var path = GetPath();
        var bytes = Encoding.UTF8.GetBytes(apiKey.Trim());
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    public static void Clear()
    {
        try
        {
            var path = GetPath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}