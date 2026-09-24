using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Daedalus.Security.Dpapi;
using Xunit;

namespace Daedalus.Security.Tests;

[SupportedOSPlatform("windows")]
public class DpapiSecretStoreTests : IDisposable
{
    private const string SecretValue = "nxmApiKey=abc123-é-🔐";

    private readonly DirectoryInfo _directory = new(
        Path.Combine(Path.GetTempPath(), $"daedalus-secret-tests-{Guid.NewGuid():N}")
    );

    public void Dispose()
    {
        _directory.Refresh();
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    private DpapiSecretStore CreateStore()
    {
        return new DpapiSecretStore(_directory);
    }

    private string GetBlobPath(string key)
    {
        return Path.Combine(_directory.FullName, key + ".secret");
    }

    [SkippableFact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        Assert.Null(await CreateStore().GetAsync("nexus-api-key"));
    }

    [SkippableFact]
    public async Task SetAndGet_RoundTripsSecret()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();
        await store.SetAsync("nexus-api-key", SecretValue);

        Assert.Equal(SecretValue, await store.GetAsync("nexus-api-key"));
    }

    [SkippableFact]
    public async Task SetAsync_ExistingKey_OverwritesValue()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();
        await store.SetAsync("nexus-api-key", SecretValue);
        await store.SetAsync("nexus-api-key", "overwritten");

        Assert.Equal("overwritten", await store.GetAsync("nexus-api-key"));
    }

    [SkippableFact]
    public async Task GetAsync_SecondStoreInstance_ReadsSameValue()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        await CreateStore().SetAsync("nexus-api-key", SecretValue);

        Assert.Equal(SecretValue, await new DpapiSecretStore(_directory).GetAsync("nexus-api-key"));
    }

    [SkippableFact]
    public async Task SetAsync_PersistsEncryptedBlobWithoutPlaintext()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();
        await store.SetAsync("nexus-api-key", SecretValue);

        var blob = await File.ReadAllBytesAsync(GetBlobPath("nexus-api-key"));
        Assert.DoesNotContain(SecretValue, Encoding.UTF8.GetString(blob), StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task GetAsync_TamperedBlob_ThrowsCryptographicException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();
        await store.SetAsync("nexus-api-key", SecretValue);

        var blobPath = GetBlobPath("nexus-api-key");
        var blob = await File.ReadAllBytesAsync(blobPath);
        blob[0] ^= 0xFF;
        await File.WriteAllBytesAsync(blobPath, blob);

        await Assert.ThrowsAsync<CryptographicException>(() => store.GetAsync("nexus-api-key"));
    }

    [SkippableFact]
    public async Task DeleteAsync_ExistingKey_RemovesSecret()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();
        await store.SetAsync("nexus-api-key", SecretValue);
        await store.DeleteAsync("nexus-api-key");

        Assert.Null(await store.GetAsync("nexus-api-key"));
        Assert.False(File.Exists(GetBlobPath("nexus-api-key")));
    }

    [SkippableFact]
    public async Task DeleteAsync_MissingKey_DoesNotThrow()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        await CreateStore().DeleteAsync("nexus-api-key");
    }

    [SkippableTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/key")]
    public async Task GetFile_InvalidKey_ThrowsArgumentException(string key)
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SetAsync(key, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(key));
    }

    [SkippableFact]
    public async Task ApplicationName_Ctor_RoundTripsUnderLocalAppData()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var applicationName = $"DaedalusSecretTests-{Guid.NewGuid():N}";
        var applicationRoot = new DirectoryInfo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName
            )
        );

        try
        {
            var store = new DpapiSecretStore(applicationName);
            await store.SetAsync("nexus-api-key", SecretValue);

            Assert.Equal(SecretValue, await store.GetAsync("nexus-api-key"));
            Assert.True(
                File.Exists(
                    Path.Combine(applicationRoot.FullName, "secrets", "nexus-api-key.secret")
                )
            );
        }
        finally
        {
            applicationRoot.Refresh();
            if (applicationRoot.Exists)
            {
                applicationRoot.Delete(recursive: true);
            }
        }
    }
}
