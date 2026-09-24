using System.Text.Json.Serialization;

namespace Daedalus.WebProtocol.Nexus;

internal sealed class SsoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public SsoResponseData? Data { get; set; }
}

internal sealed class SsoResponseData
{
    [JsonPropertyName("connection_token")]
    public string? ConnectionToken { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }
}
