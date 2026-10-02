using Xunit;

namespace Ariadne.WebProtocol.Nexus.Tests;

public class NexusRequestTests
{
    [Fact]
    public void Create_AddsApiKeyAndApplicationIdentityHeaders()
    {
        using var request = NexusRequest.Create(
            HttpMethod.Get,
            new Uri("https://api.nexusmods.com/v1/users/validate"),
            "test-key"
        );

        Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("apikey")));
        Assert.Equal("Ariadne", Assert.Single(request.Headers.GetValues("Application-Name")));
        Assert.False(
            string.IsNullOrWhiteSpace(
                Assert.Single(request.Headers.GetValues("Application-Version"))
            )
        );
    }
}
