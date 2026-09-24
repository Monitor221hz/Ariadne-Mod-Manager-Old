using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.WebProtocol.Nexus;

public sealed class NexusDownloadResolver : INexusDownloadResolver
{
    private const string CdnServerName = "Nexus CDN";
    private static readonly Uri ApiBaseUri = new("https://api.nexusmods.com/v1/");
    private static readonly HttpClient DefaultClient = new();

    private readonly HttpClient _client;

    public NexusDownloadResolver(HttpClient? client = null)
    {
        _client = client ?? DefaultClient;
    }

    public int? DailyRequestsLimit { get; private set; }
    public int? DailyRequestsRemaining { get; private set; }

    public async Task<NexusDownloadLink?> ResolveDownloadAsync(
        NxmModLink link,
        string apiKey,
        bool isPremium,
        string? serverName = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var requestUri = new Uri(
                ApiBaseUri,
                $"games/{link.GameDomain}/mods/{link.ModId}/files/{link.FileId}/download_link.json"
                    + $"?key={Uri.EscapeDataString(link.Key)}&expires={link.Expires}"
            );

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("apikey", apiKey);

            using var response = await _client.SendAsync(request, cancellationToken);
            UpdateRequestLimits(response.Headers);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var links = JsonSerializer.Deserialize<List<DownloadLinkRecord>>(payload);
            if (links is null || links.Count == 0)
            {
                return null;
            }

            var preferredServer =
                isPremium && !string.IsNullOrEmpty(serverName) ? serverName : CdnServerName;
            var match = links.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ShortName,
                    preferredServer,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (match?.Uri is not { Length: > 0 })
            {
                return null;
            }
            if (!Uri.TryCreate(match.Uri, UriKind.Absolute, out var downloadUri))
            {
                return null;
            }

            return new NexusDownloadLink(match.Name, match.ShortName, downloadUri);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private void UpdateRequestLimits(HttpResponseHeaders headers)
    {
        if (
            TryGetHeader(headers, "x-rl-daily-limit", out var limit)
            && int.TryParse(limit, out var parsedLimit)
        )
        {
            DailyRequestsLimit = parsedLimit;
        }
        if (
            TryGetHeader(headers, "x-rl-daily-remaining", out var remaining)
            && int.TryParse(remaining, out var parsedRemaining)
        )
        {
            DailyRequestsRemaining = parsedRemaining;
        }
    }

    private static bool TryGetHeader(HttpResponseHeaders headers, string name, out string value)
    {
        value = string.Empty;
        if (!headers.TryGetValues(name, out var values))
        {
            return false;
        }
        value = values.FirstOrDefault() ?? string.Empty;
        return value.Length > 0;
    }

    private sealed record DownloadLinkRecord(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("short_name")] string? ShortName,
        [property: JsonPropertyName("URI")] string? Uri
    );
}
