using System.Net.WebSockets;

namespace Daedalus.WebProtocol.Nexus;

public interface IWebSocketConnection : IDisposable
{
    WebSocketState State { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    Task SendAsync(
        ArraySegment<byte> payload,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken
    );

    Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken
    );

    Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken
    );
}
