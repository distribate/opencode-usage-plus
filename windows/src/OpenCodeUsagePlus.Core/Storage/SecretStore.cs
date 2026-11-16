using System.Security.Cryptography;
using System.Text;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>
/// Stores the optional "sk-..." API key in the OS secret store. On Windows that is DPAPI scoped to
/// the current user, so the file is useless after a copy to another account or machine.
/// </summary>
public sealed class SecretStore
{
    private readonly string _filePath;

    public SecretStore(string? filePath = null) => _filePath = filePath ?? AppPaths.ApiKeyFile;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public string? Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var protectedBytes = File.ReadAllBytes(_filePath);
            var plain = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var text = Encoding.UTF8.GetString(plain).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"api key read failed: {ex.Message}");
            return null;
        }
    }

    public bool TrySave(string apiKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            AppPaths.EnsureExists();
            var plain = Encoding.UTF8.GetBytes(apiKey.Trim());
            var protectedBytes = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var temp = _filePath + ".tmp";
            File.WriteAllBytes(temp, protectedBytes);
            File.Move(temp, _filePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"api key save failed: {ex.Message}");
            return false;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"api key delete failed: {ex.Message}");
        }
    }
}
