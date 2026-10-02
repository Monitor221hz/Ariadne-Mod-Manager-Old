using System.Reflection;

namespace Ariadne.WebProtocol.Nexus;

public static class NexusRequest
{
    private static readonly string ApplicationVersion = ResolveVersion();

    public static HttpRequestMessage Create(HttpMethod method, Uri uri, string apiKey)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("apikey", apiKey);
        request.Headers.Add("Application-Name", "Ariadne");
        request.Headers.Add("Application-Version", ApplicationVersion);
        return request;
    }

    private static string ResolveVersion()
    {
        var assembly = typeof(NexusRequest).Assembly;
        return assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
