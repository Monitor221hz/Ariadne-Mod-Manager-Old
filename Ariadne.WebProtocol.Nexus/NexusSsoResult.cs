namespace Ariadne.WebProtocol.Nexus;

public sealed record NexusSsoResult
{
    public string? ApiKey { get; }
    public string? Error { get; }
    public bool Succeeded => ApiKey is not null;

    private NexusSsoResult(string? apiKey, string? error)
    {
        ApiKey = apiKey;
        Error = error;
    }

    public static NexusSsoResult Authorized(string apiKey)
    {
        return new NexusSsoResult(apiKey, null);
    }

    public static NexusSsoResult Failed(string error)
    {
        return new NexusSsoResult(null, error);
    }
}
