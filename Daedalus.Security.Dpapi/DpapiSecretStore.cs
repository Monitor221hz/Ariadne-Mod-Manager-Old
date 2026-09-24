using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Daedalus.Security.Dpapi;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private const string FileExtension = ".secret";

    private readonly DirectoryInfo _directory;

    public DpapiSecretStore(string applicationName)
        : this(
            new DirectoryInfo(
                Path.Join(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    applicationName,
                    "secrets"
                )
            )
        ) { }

    public DpapiSecretStore(DirectoryInfo directory)
    {
        _directory = directory;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var file = GetFile(key);
        file.Refresh();
        if (!file.Exists)
        {
            return Task.FromResult<string?>(null);
        }

        var protectedBytes = File.ReadAllBytes(file.FullName);
        var plainBytes = ProtectedData.Unprotect(
            protectedBytes,
            null,
            DataProtectionScope.CurrentUser
        );
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var file = GetFile(key);
        _directory.Create();

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(
            plainBytes,
            null,
            DataProtectionScope.CurrentUser
        );
        File.WriteAllBytes(file.FullName, protectedBytes);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var file = GetFile(key);
        file.Refresh();
        if (file.Exists)
        {
            file.Delete();
        }
        return Task.CompletedTask;
    }

    private FileInfo GetFile(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key must not be empty.", nameof(key));
        }
        if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"Secret key \"{key}\" is not a valid file name.",
                nameof(key)
            );
        }

        return new FileInfo(Path.Join(_directory.FullName, key + FileExtension));
    }
}
