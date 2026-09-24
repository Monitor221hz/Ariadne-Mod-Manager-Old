using System.Net;
using System.Text;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusFileClientTests
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

    private static HttpResponseMessage Json(HttpStatusCode status, string content)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private static NxmModLink SampleLink()
    {
        return (NxmModLink)
            NxmLink.Parse(
                "nxm://skyrimspecialedition/mods/12604/files/35407?key=abc&expires=1893456000&user_id=1"
            );
    }

    [Fact]
    public async Task GetFileAsync_ValidResponse_MapsMetadata()
    {
        var handler = new StubHandler(
            Json(
                HttpStatusCode.OK,
                "{\"file_id\":35407,\"name\":\"SkyUI_5_2_SE\",\"version\":\"5.2SE\",\"file_name\":\"SkyUI_5_2_SE-12604-5-2SE.7z\",\"size_in_bytes\":2783417}"
            )
        );
        var client = new NexusFileClient(new HttpClient(handler));

        var metadata = await client.GetFileAsync(SampleLink(), "the-key");

        Assert.NotNull(metadata);
        Assert.Equal(35407, metadata.FileId);
        Assert.Equal("SkyUI_5_2_SE", metadata.Name);
        Assert.Equal("SkyUI_5_2_SE-12604-5-2SE.7z", metadata.FileName);
        Assert.Equal("5.2SE", metadata.Version);
        Assert.Equal(2783417, metadata.SizeInBytes);
    }

    [Fact]
    public async Task GetFileAsync_SendsExpectedRequest()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "{\"file_id\":35407}"));
        var client = new NexusFileClient(new HttpClient(handler));

        await client.GetFileAsync(SampleLink(), "the-key");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://api.nexusmods.com/v1/games/skyrimspecialedition/mods/12604/files/35407.json",
            handler.LastRequest.RequestUri?.AbsoluteUri
        );
        Assert.Equal("the-key", handler.LastRequest.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task GetFileAsync_UnknownFile_ReturnsNull()
    {
        var handler = new StubHandler(
            Json(HttpStatusCode.NotFound, "{\"error\":\"File ID '1' not found\"}")
        );
        var client = new NexusFileClient(new HttpClient(handler));

        Assert.Null(await client.GetFileAsync(SampleLink(), "the-key"));
    }

    [Fact]
    public async Task GetFileAsync_MalformedJson_ReturnsNull()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "not json"));
        var client = new NexusFileClient(new HttpClient(handler));

        Assert.Null(await client.GetFileAsync(SampleLink(), "the-key"));
    }
}
