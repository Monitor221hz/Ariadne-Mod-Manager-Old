using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.Downloads;

public sealed record DownloadManifest
{
    [JsonPropertyName("repository")]
    public required string Repository { get; init; }

    [JsonPropertyName("game")]
    public string? Game { get; init; }

    [JsonPropertyName("modId")]
    public int? ModId { get; init; }

    [JsonPropertyName("fileId")]
    public int? FileId { get; init; }

    [JsonPropertyName("modName")]
    public string? ModName { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("sizeInBytes")]
    public long? SizeInBytes { get; init; }

    [JsonPropertyName("sourceLink")]
    public string? SourceLink { get; init; }

    [JsonPropertyName("resolvedUrl")]
    public string? ResolvedUrl { get; init; }

    [JsonPropertyName("downloadedUtc")]
    public required DateTimeOffset DownloadedUtc { get; init; }
}

public static class DownloadManifestStore
{
    private const string ManifestExtension = ".manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static FileInfo GetManifestPath(FileInfo downloaded)
    {
        return new FileInfo(downloaded.FullName + ManifestExtension);
    }

    public static void Write(FileInfo downloaded, DownloadManifest manifest)
    {
        var path = GetManifestPath(downloaded);
        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        path.Directory?.Create();
        File.WriteAllText(path.FullName, json);
    }

    public static DownloadManifest? TryRead(FileInfo downloaded)
    {
        var path = GetManifestPath(downloaded);
        path.Refresh();
        if (!path.Exists)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DownloadManifest>(
                File.ReadAllText(path.FullName),
                SerializerOptions
            );
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
}
