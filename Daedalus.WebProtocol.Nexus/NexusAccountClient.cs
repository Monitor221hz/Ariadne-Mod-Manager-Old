using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.WebProtocol.Nexus;

public sealed class NexusAccountClient : INexusAccountClient
{
    private static readonly Uri ApiBaseUri = new("https://api.nexusmods.com/v1/");
    private static readonly JsonSerializerOptions SerializerOptions = new();
    private static readonly HttpClient DefaultClient = new();

    private readonly HttpClient _client;

    public NexusAccountClient(HttpClient? client = null)
    {
        _client = client ?? DefaultClient;
    }

    public async Task<NexusAccount?> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(ApiBaseUri, "users/validate")
            );
            request.Headers.Add("apikey", apiKey);

            using var response = await _client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var validation = JsonSerializer.Deserialize<ValidateResponse>(
                payload,
                SerializerOptions
            );
            if (validation is null || validation.Message is { Length: > 0 })
            {
                return null;
            }
            if (validation.Name is not { Length: > 0 })
            {
                return null;
            }

            return new NexusAccount(validation.Name, validation.IsPremium, validation.ProfileUrl);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private sealed record ValidateResponse(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("is_premium")] bool IsPremium,
        [property: JsonPropertyName("profile_url")] string? ProfileUrl,
        [property: JsonPropertyName("message")] string? Message
    );
}
