using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariadne.WebProtocol.Nexus;

public sealed class NexusFileClient : INexusFileClient
{
    private static readonly Uri ApiBaseUri = new("https://api.nexusmods.com/v1/");
    private static readonly HttpClient DefaultClient = new();

    private readonly HttpClient _client;

    public NexusFileClient(HttpClient? client = null)
    {
        _client = client ?? DefaultClient;
    }

    public async Task<NexusFileMetadata?> GetFileAsync(
        NxmModLink link,
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var requestUri = new Uri(
                ApiBaseUri,
                $"games/{link.GameDomain}/mods/{link.ModId}/files/{link.FileId}.json"
            );

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("apikey", apiKey);

            using var response = await _client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var record = JsonSerializer.Deserialize<FileResponse>(payload);
            if (record is null)
            {
                return null;
            }

            return new NexusFileMetadata(
                record.FileId,
                record.Name,
                record.FileName,
                record.Version,
                record.SizeInBytes
            );
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private sealed record FileResponse(
        [property: JsonPropertyName("file_id")] int FileId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("size_in_bytes")] long? SizeInBytes
    );
}
