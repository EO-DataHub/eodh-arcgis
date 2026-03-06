using System.IO;
using eodh.Services;
using Xunit;

namespace eodh.Tests.Services;

public class CredentialStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CredentialStore _store;

    public CredentialStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "eodh-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new CredentialStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSameCredentials()
    {
        _store.Save("testuser", "secret-api-token-123", "production");

        var result = _store.Load();

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Value.Username);
        Assert.Equal("secret-api-token-123", result.Value.ApiToken);
        Assert.Equal("production", result.Value.Environment);
    }

    [Fact]
    public void Load_ReturnsNull_WhenNoFileExists()
    {
        var result = _store.Load();

        Assert.Null(result);
    }

    [Fact]
    public void Delete_RemovesSavedCredentials()
    {
        _store.Save("testuser", "token", "staging");
        _store.Delete();

        var result = _store.Load();

        Assert.Null(result);
    }
}
