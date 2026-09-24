using System.Net.WebSockets;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusSsoSessionTests
{
    private static NexusSsoSession CreateSession(
        ScriptedWebSocketConnection socket,
        string applicationSlug = "daedalus"
    )
    {
        return new NexusSsoSession(applicationSlug, new ScriptedWebSocketConnectionFactory(socket));
    }

    [Fact]
    public void AuthorizationUri_ContainsSessionIdAndApplicationSlug()
    {
        var session = new NexusSsoSession("daedalus");
        var uri = session.AuthorizationUri.ToString();

        Assert.StartsWith("https://www.nexusmods.com/sso?id=", uri);
        Assert.EndsWith("&application=daedalus", uri);
        Assert.True(Guid.TryParse(uri.Split("id=")[1].Split('&')[0], out _));
    }

    [Fact]
    public void AuthorizationUri_IsUniquePerSession()
    {
        var first = new NexusSsoSession("daedalus");
        var second = new NexusSsoSession("daedalus");

        Assert.NotEqual(first.AuthorizationUri, second.AuthorizationUri);
    }

    [Fact]
    public async Task ConnectAsync_ApiKeyIssued_ReturnsAuthorizedKey()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("{\"success\":true,\"data\":{\"connection_token\":\"tok\"}}");
        socket.EnqueueText("{\"success\":true,\"data\":{\"api_key\":\"KEY123\"}}");

        var result = await CreateSession(socket).ConnectAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("KEY123", result.ApiKey);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ConnectAsync_FragmentedKeyFrame_ReassemblesMessage()
    {
        var socket = new ScriptedWebSocketConnection();
        const string message = "{\"success\":true,\"data\":{\"api_key\":\"FRAGMENTED_KEY\"}}";
        var splitAt = message.Length / 3;
        socket.EnqueueText(message[..splitAt], endOfMessage: false);
        socket.EnqueueText(message[splitAt..(splitAt * 2)], endOfMessage: false);
        socket.EnqueueText(message[(splitAt * 2)..]);

        var result = await CreateSession(socket).ConnectAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("FRAGMENTED_KEY", result.ApiKey);
    }

    [Fact]
    public async Task ConnectAsync_SendsHandshakeWithSessionIdAndProtocolVersion()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("{\"success\":true,\"data\":{\"api_key\":\"KEY\"}}");
        var session = CreateSession(socket);

        await session.ConnectAsync();

        var handshake = Assert.Single(socket.SentText);
        Assert.Contains("\"protocol\":2", handshake);
        Assert.Contains(session.AuthorizationUri.Query.Split("id=")[1].Split('&')[0], handshake);
        Assert.Equal(new Uri("wss://sso.nexusmods.com"), socket.ConnectedTo);
    }

    [Fact]
    public async Task ConnectAsync_KeyReceived_ClosesAndDisposesSocket()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("{\"success\":true,\"data\":{\"api_key\":\"KEY\"}}");

        await CreateSession(socket).ConnectAsync();

        Assert.True(socket.CloseRequested);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task ConnectAsync_RequestRejected_ReturnsFailure()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("{\"success\":false}");

        var result = await CreateSession(socket).ConnectAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.ApiKey);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ConnectAsync_ServerClosesBeforeKey_ReturnsFailure()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("{\"success\":true,\"data\":{\"connection_token\":\"tok\"}}");
        socket.EnqueueClose();

        var result = await CreateSession(socket).ConnectAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ConnectAsync_MalformedMessage_ReturnsFailure()
    {
        var socket = new ScriptedWebSocketConnection();
        socket.EnqueueText("not json at all");

        var result = await CreateSession(socket).ConnectAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ConnectAsync_SocketFailsToConnect_ReturnsFailure()
    {
        var socket = new ScriptedWebSocketConnection
        {
            ConnectException = new WebSocketException("boom"),
        };

        var result = await CreateSession(socket).ConnectAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public async Task ConnectAsync_Cancelled_PropagatesCancellation()
    {
        var socket = new ScriptedWebSocketConnection(blockWhenEmpty: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSession(socket).ConnectAsync(cts.Token)
        );
    }
}
