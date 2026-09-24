using System.Net.WebSockets;

namespace Daedalus.WebProtocol.Nexus;

public sealed class ClientWebSocketConnection : IWebSocketConnection
{
    private readonly ClientWebSocket _socket = new();

    public WebSocketState State => _socket.State;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        return _socket.ConnectAsync(uri, cancellationToken);
    }

    public Task SendAsync(
        ArraySegment<byte> payload,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken
    )
    {
        return _socket.SendAsync(payload, messageType, endOfMessage, cancellationToken);
    }

    public Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        return _socket.ReceiveAsync(buffer, cancellationToken);
    }

    public Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken
    )
    {
        return _socket.CloseAsync(closeStatus, statusDescription, cancellationToken);
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}
