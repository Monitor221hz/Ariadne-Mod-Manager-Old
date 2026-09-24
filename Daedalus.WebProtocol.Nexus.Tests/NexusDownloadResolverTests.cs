using System.Net;
using System.Text;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusDownloadResolverTests
{
    private const string CdnLinks =
        "["
        + "{\"name\":\"Nexus CDN\",\"short_name\":\"Nexus CDN\",\"URI\":\"https://cdn.nexusmods.com/cdn.7z\"},"
        + "{\"name\":\"Prague\",\"short_name\":\"Prague\",\"URI\":\"https://cdn.nexusmods.com/prague.7z\"}"
        + "]";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private static NxmModLink SampleModLink()
    {
        return (NxmModLink)
            NxmLink.Parse(
                "nxm://skyrimspecialedition/mods/1234/files/56789?key=abc&expires=1893456000&user_id=42"
            );
    }

    private static NexusDownloadResolver CreateResolver(StubHandler handler)
    {
        return new NexusDownloadResolver(new HttpClient(handler));
    }

    [Fact]
    public async Task ResolveDownloadAsync_NonPremium_AlwaysUsesCdn()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(
                SampleModLink(),
                "the-key",
                isPremium: false,
                serverName: "Prague"
            );

        Assert.NotNull(resolved);
        Assert.Equal("https://cdn.nexusmods.com/cdn.7z", resolved.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveDownloadAsync_PremiumWithServer_UsesRequestedServer()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(
                SampleModLink(),
                "the-key",
                isPremium: true,
                serverName: "Prague"
            );

        Assert.NotNull(resolved);
        Assert.Equal("https://cdn.nexusmods.com/prague.7z", resolved.Uri.AbsoluteUri);
        Assert.Equal("Prague", resolved.ShortName);
    }

    [Fact]
    public async Task ResolveDownloadAsync_PremiumWithoutServer_FallsBackToCdn()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: true);

        Assert.NotNull(resolved);
        Assert.Equal("Nexus CDN", resolved.ShortName);
    }

    [Fact]
    public async Task ResolveDownloadAsync_SendsKeyExpiresAndApiKey()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));

        await CreateResolver(handler)
            .ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: true);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://api.nexusmods.com/v1/games/skyrimspecialedition/mods/1234/files/56789/download_link.json?key=abc&expires=1893456000",
            handler.LastRequest.RequestUri?.AbsoluteUri
        );
        Assert.Equal("the-key", handler.LastRequest.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task ResolveDownloadAsync_KeyWithReservedCharacters_IsEscaped()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));
        var link = new NxmModLink
        {
            GameDomain = "skyrim",
            ModId = 1,
            FileId = 2,
            Key = "a&b c",
            Expires = 1,
            UserId = 2,
        };

        await CreateResolver(handler).ResolveDownloadAsync(link, "the-key", isPremium: false);

        Assert.Contains("key=a%26b%20c", handler.LastRequest!.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveDownloadAsync_ServerNotOffered_ReturnsNull()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, CdnLinks));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(
                SampleModLink(),
                "the-key",
                isPremium: true,
                serverName: "Zurich"
            );

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveDownloadAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(Json(HttpStatusCode.NotFound, "{}"));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: false);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveDownloadAsync_EmptyList_ReturnsNull()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "[]"));

        var resolved = await CreateResolver(handler)
            .ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: false);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveDownloadAsync_MalformedJson_ReturnsNull()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "not json"));

        Assert.Null(
            await CreateResolver(handler)
                .ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: false)
        );
    }

    [Fact]
    public async Task ResolveDownloadAsync_RateLimitHeaders_AreExposed()
    {
        var response = Json(HttpStatusCode.OK, CdnLinks);
        response.Headers.Add("x-rl-daily-limit", "500");
        response.Headers.Add("x-rl-daily-remaining", "412");
        var resolver = CreateResolver(new StubHandler(response));

        await resolver.ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: false);

        Assert.Equal(500, resolver.DailyRequestsLimit);
        Assert.Equal(412, resolver.DailyRequestsRemaining);
    }

    [Fact]
    public async Task ResolveDownloadAsync_MissingRateLimitHeaders_LeavesLimitsUnset()
    {
        var resolver = CreateResolver(new StubHandler(Json(HttpStatusCode.OK, CdnLinks)));

        await resolver.ResolveDownloadAsync(SampleModLink(), "the-key", isPremium: false);

        Assert.Null(resolver.DailyRequestsLimit);
        Assert.Null(resolver.DailyRequestsRemaining);
    }
}
