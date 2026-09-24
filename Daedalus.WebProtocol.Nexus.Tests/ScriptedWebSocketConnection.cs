using System.Net.WebSockets;
using System.Text;

namespace Daedalus.WebProtocol.Nexus.Tests;

internal sealed class ScriptedWebSocketConnection : IWebSocketConnection
{
    private sealed record Frame(byte[] Payload, bool EndOfMessage, bool IsClose);

    private readonly Queue<Frame> _incoming = new();
    private readonly bool _blockWhenEmpty;

    public List<string> SentText { get; } = new();
    public Uri? ConnectedTo { get; private set; }
    public bool CloseRequested { get; private set; }
    public bool Disposed { get; private set; }

    internal WebSocketException? ConnectException { get; set; }

    public WebSocketState State => WebSocketState.Open;

    public ScriptedWebSocketConnection(bool blockWhenEmpty = false)
    {
        _blockWhenEmpty = blockWhenEmpty;
    }

    public void EnqueueText(string text, bool endOfMessage = true)
    {
        _incoming.Enqueue(new Frame(Encoding.UTF8.GetBytes(text), endOfMessage, false));
    }

    public void EnqueueClose()
    {
        _incoming.Enqueue(new Frame([], true, true));
    }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ConnectException is not null)
        {
            throw ConnectException;
        }
        ConnectedTo = uri;
        return Task.CompletedTask;
    }

    public Task SendAsync(
        ArraySegment<byte> payload,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken
    )
    {
        SentText.Add(Encoding.UTF8.GetString(payload));
        return Task.CompletedTask;
    }

    public async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        if (_incoming.Count == 0)
        {
            if (_blockWhenEmpty)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            throw new InvalidOperationException("No scripted frames remain.");
        }

        var frame = _incoming.Dequeue();
        if (frame.IsClose)
        {
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }
        frame.Payload.AsSpan().CopyTo(buffer);
        return new WebSocketReceiveResult(
            frame.Payload.Length,
            WebSocketMessageType.Text,
            frame.EndOfMessage
        );
    }

    public Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken
    )
    {
        CloseRequested = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Disposed = true;
    }
}

internal sealed class ScriptedWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    private readonly ScriptedWebSocketConnection _connection;

    public ScriptedWebSocketConnectionFactory(ScriptedWebSocketConnection connection)
    {
        _connection = connection;
    }

    public IWebSocketConnection Create()
    {
        return _connection;
    }
}
