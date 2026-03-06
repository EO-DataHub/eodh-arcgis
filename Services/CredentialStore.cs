using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace eodh.Services;

/// <summary>
/// Persists EODH credentials to disk with DPAPI encryption for the API token.
/// Stored in %LocalAppData%\EodhArcGis\credentials.dat (same root as ThumbnailCache).
/// </summary>
public class CredentialStore
{
    private readonly string _filePath;

    public CredentialStore(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EodhArcGis");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "credentials.dat");
    }

    public void Save(string username, string apiToken, string environment)
    {
        var encryptedToken = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiToken), null, DataProtectionScope.CurrentUser);

        var data = new StoredCredentials
        {
            Username = username,
            EncryptedToken = Convert.ToBase64String(encryptedToken),
            Environment = environment
        };

        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(_filePath, json);
    }

    public (string Username, string ApiToken, string Environment)? Load()
    {
        if (!File.Exists(_filePath)) return null;

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<StoredCredentials>(json);
            if (data?.Username == null || data.EncryptedToken == null || data.Environment == null)
                return null;

            var tokenBytes = ProtectedData.Unprotect(
                Convert.FromBase64String(data.EncryptedToken), null, DataProtectionScope.CurrentUser);

            return (data.Username, Encoding.UTF8.GetString(tokenBytes), data.Environment);
        }
        catch
        {
            return null;
        }
    }

    public void Delete()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    private class StoredCredentials
    {
        public string? Username { get; set; }
        public string? EncryptedToken { get; set; }
        public string? Environment { get; set; }
    }
}
