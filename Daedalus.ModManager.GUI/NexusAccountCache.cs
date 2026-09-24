using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daedalus.WebProtocol.Nexus;

namespace Daedalus.ModManager.GUI;

public interface INexusAccountCache
{
    NexusAccount? Read(string apiKey);

    void Write(string apiKey, NexusAccount account);

    void Clear();
}

public sealed class NexusAccountCache : INexusAccountCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private readonly FileInfo _file;

    public NexusAccountCache(string applicationName)
    {
        _file = new FileInfo(
            Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName,
                "nexus-account.json"
            )
        );
    }

    public NexusAccount? Read(string apiKey)
    {
        _file.Refresh();
        if (!_file.Exists)
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<CachedAccount>(
                File.ReadAllText(_file.FullName),
                SerializerOptions
            );
            if (stored is null || stored.KeyFingerprint != Fingerprint(apiKey))
            {
                return null;
            }
            return new NexusAccount(stored.Name, stored.IsPremium, stored.ProfileUrl);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Write(string apiKey, NexusAccount account)
    {
        try
        {
            _file.Directory?.Create();
            File.WriteAllText(
                _file.FullName,
                JsonSerializer.Serialize(
                    new CachedAccount(
                        Fingerprint(apiKey),
                        account.Name,
                        account.IsPremium,
                        account.ProfileUrl
                    ),
                    SerializerOptions
                )
            );
        }
        catch (IOException) { }
    }

    public void Clear()
    {
        _file.Refresh();
        if (_file.Exists)
        {
            try
            {
                _file.Delete();
            }
            catch (IOException) { }
        }
    }

    private static string Fingerprint(string apiKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
    }

    private sealed record CachedAccount(
        [property: JsonPropertyName("keyFingerprint")] string KeyFingerprint,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("isPremium")] bool IsPremium,
        [property: JsonPropertyName("profileUrl")] string? ProfileUrl
    );
}
