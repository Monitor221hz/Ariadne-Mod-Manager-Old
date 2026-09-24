using System.Net;
using System.Text;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusAccountClientTests
{
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

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    [Fact]
    public async Task ValidateAsync_ValidResponse_ReturnsAccount()
    {
        var handler = new StubHandler(
            JsonResponse(
                HttpStatusCode.OK,
                "{\"name\":\"Ron Example\",\"is_premium\":true,\"profile_url\":\"https://www.nexusmods.com/users/1\"}"
            )
        );
        var client = new NexusAccountClient(new HttpClient(handler));

        var account = await client.ValidateAsync("the-key");

        Assert.NotNull(account);
        Assert.Equal("Ron Example", account.Name);
        Assert.True(account.IsPremium);
        Assert.Equal("https://www.nexusmods.com/users/1", account.ProfileUrl);
    }

    [Fact]
    public async Task ValidateAsync_SendsApiKeyHeader()
    {
        var handler = new StubHandler(
            JsonResponse(HttpStatusCode.OK, "{\"name\":\"Ron\",\"is_premium\":false}")
        );
        var client = new NexusAccountClient(new HttpClient(handler));

        await client.ValidateAsync("the-key");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("the-key", handler.LastRequest.Headers.GetValues("apikey").Single());
        Assert.Equal(
            new Uri("https://api.nexusmods.com/v1/users/validate"),
            handler.LastRequest.RequestUri
        );
    }

    [Fact]
    public async Task ValidateAsync_Unauthorized_ReturnsNull()
    {
        var handler = new StubHandler(
            JsonResponse(HttpStatusCode.Unauthorized, "{\"message\":\"Invalid Key\"}")
        );
        var client = new NexusAccountClient(new HttpClient(handler));

        Assert.Null(await client.ValidateAsync("bad-key"));
    }

    [Fact]
    public async Task ValidateAsync_MessagePresent_ReturnsNull()
    {
        var handler = new StubHandler(
            JsonResponse(
                HttpStatusCode.OK,
                "{\"name\":\"Ron\",\"is_premium\":false,\"message\":\"oink\"}"
            )
        );
        var client = new NexusAccountClient(new HttpClient(handler));

        Assert.Null(await client.ValidateAsync("the-key"));
    }

    [Fact]
    public async Task ValidateAsync_MissingName_ReturnsNull()
    {
        var handler = new StubHandler(JsonResponse(HttpStatusCode.OK, "{\"is_premium\":false}"));
        var client = new NexusAccountClient(new HttpClient(handler));

        Assert.Null(await client.ValidateAsync("the-key"));
    }

    [Fact]
    public async Task ValidateAsync_MalformedJson_ReturnsNull()
    {
        var handler = new StubHandler(JsonResponse(HttpStatusCode.OK, "not json"));
        var client = new NexusAccountClient(new HttpClient(handler));

        Assert.Null(await client.ValidateAsync("the-key"));
    }
}
