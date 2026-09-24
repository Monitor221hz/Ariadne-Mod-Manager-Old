using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.WebProtocol.Nexus;

public sealed class NexusSsoSession : INexusSsoSession
{
    private static readonly Uri SsoEndpoint = new("wss://sso.nexusmods.com");
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);

    private const int ProtocolVersion = 2;
    private const int ReceiveBufferSize = 4096;

    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly string _applicationSlug;
    private readonly IWebSocketConnectionFactory _socketFactory;

    public NexusSsoSession(string applicationSlug)
        : this(applicationSlug, new ClientWebSocketConnectionFactory()) { }

    public NexusSsoSession(string applicationSlug, IWebSocketConnectionFactory socketFactory)
    {
        _applicationSlug = applicationSlug;
        _socketFactory = socketFactory;
    }

    public Uri AuthorizationUri =>
        new($"https://www.nexusmods.com/sso?id={_sessionId}&application={_applicationSlug}");

    public async Task<NexusSsoResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var socket = _socketFactory.Create();
        try
        {
            await socket.ConnectAsync(SsoEndpoint, cancellationToken);
            await SendHandshakeAsync(socket, cancellationToken);

            using var pingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            var pingTask = PingLoopAsync(socket, pingCancellation.Token);
            try
            {
                return await ReceiveResultAsync(socket, cancellationToken);
            }
            finally
            {
                await pingCancellation.CancelAsync();
                await pingTask;
                await CloseAsync(socket);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is WebSocketException or IOException or JsonException)
        {
            return NexusSsoResult.Failed(exception.Message);
        }
    }

    private async Task SendHandshakeAsync(
        IWebSocketConnection socket,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new SsoHandshake(_sessionId, null, ProtocolVersion)
        );
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<NexusSsoResult> ReceiveResultAsync(
        IWebSocketConnection socket,
        CancellationToken cancellationToken
    )
    {
        var segment = new ArraySegment<byte>(new byte[ReceiveBufferSize]);
        var message = new ArrayBufferWriter<byte>();

        while (socket.State is WebSocketState.Open)
        {
            message.Clear();
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(segment, cancellationToken);
                if (frame.MessageType is WebSocketMessageType.Close)
                {
                    return NexusSsoResult.Failed(
                        "The server closed the connection before issuing an API key."
                    );
                }
                message.Write(segment.AsSpan(0, frame.Count));
            } while (!frame.EndOfMessage);

            var response = JsonSerializer.Deserialize<SsoResponse>(message.WrittenSpan);
            if (response is not { Success: true, Data: not null })
            {
                return NexusSsoResult.Failed("The server rejected the authorization request.");
            }
            if (response.Data.ApiKey is not null)
            {
                return NexusSsoResult.Authorized(response.Data.ApiKey);
            }
        }

        return NexusSsoResult.Failed("The connection closed before an API key was issued.");
    }

    private static async Task PingLoopAsync(
        IWebSocketConnection socket,
        CancellationToken cancellationToken
    )
    {
        using var timer = new PeriodicTimer(PingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (socket.State is not WebSocketState.Open)
                {
                    return;
                }
                await socket.SendAsync(
                    ArraySegment<byte>.Empty,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken
                );
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private static async Task CloseAsync(IWebSocketConnection socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }
        try
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                null,
                CancellationToken.None
            );
        }
        catch (WebSocketException) { }
    }

    private sealed record SsoHandshake(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("protocol")] int Protocol
    );
}
