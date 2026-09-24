using Daedalus.Security.Libsecret;
using Xunit;

namespace Daedalus.Security.Tests;

public class LibsecretSecretStoreTests
{
    private const string SecretValue = "nxmApiKey=abc123-é-🔐";

    private static readonly bool SecretToolAvailable = FindOnPath("secret-tool");

    private static bool FindOnPath(string executable)
    {
        var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnvironment))
        {
            return false;
        }

        foreach (var directory in pathEnvironment.Split(Path.PathSeparator))
        {
            if (directory.Length is 0)
            {
                continue;
            }
            try
            {
                if (File.Exists(Path.Combine(directory, executable)))
                {
                    return true;
                }
            }
            catch (ArgumentException) { }
        }

        return false;
    }

    private static LibsecretSecretStore CreateStore(out string applicationName)
    {
        applicationName = $"daedalus-tests-{Guid.NewGuid():N}";
        return new LibsecretSecretStore(applicationName);
    }

    [SkippableFact]
    public async Task GetAsync_MissingSecretTool_ThrowsInformativeError()
    {
        Skip.If(SecretToolAvailable, "secret-tool is available here");

        var store = CreateStore(out _);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync("nexus-api-key")
        );
        Assert.Contains("libsecret", exception.Message);
    }

    [SkippableFact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        Skip.IfNot(SecretToolAvailable, "requires secret-tool");

        var store = CreateStore(out _);

        Assert.Null(await store.GetAsync($"key-{Guid.NewGuid():N}"));
    }

    [SkippableFact]
    public async Task SetAndGet_RoundTripsSecret()
    {
        Skip.IfNot(SecretToolAvailable, "requires secret-tool");

        var store = CreateStore(out _);
        var key = $"key-{Guid.NewGuid():N}";
        try
        {
            await store.SetAsync(key, SecretValue);

            Assert.Equal(SecretValue, await store.GetAsync(key));
        }
        finally
        {
            await store.DeleteAsync(key);
        }
    }

    [SkippableFact]
    public async Task SetAsync_ExistingKey_OverwritesValue()
    {
        Skip.IfNot(SecretToolAvailable, "requires secret-tool");

        var store = CreateStore(out _);
        var key = $"key-{Guid.NewGuid():N}";
        try
        {
            await store.SetAsync(key, SecretValue);
            await store.SetAsync(key, "overwritten");

            Assert.Equal("overwritten", await store.GetAsync(key));
        }
        finally
        {
            await store.DeleteAsync(key);
        }
    }

    [SkippableFact]
    public async Task DeleteAsync_ExistingKey_RemovesSecret()
    {
        Skip.IfNot(SecretToolAvailable, "requires secret-tool");

        var store = CreateStore(out _);
        var key = $"key-{Guid.NewGuid():N}";

        await store.SetAsync(key, SecretValue);
        await store.DeleteAsync(key);

        Assert.Null(await store.GetAsync(key));
    }

    [SkippableFact]
    public async Task DeleteAsync_MissingKey_DoesNotThrow()
    {
        Skip.IfNot(SecretToolAvailable, "requires secret-tool");

        var store = CreateStore(out _);

        await store.DeleteAsync($"key-{Guid.NewGuid():N}");
    }
}
