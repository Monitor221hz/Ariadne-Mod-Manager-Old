using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Ariadne.Downloads.Tests;

internal sealed class StubHttpServer : IDisposable
{
    private static readonly Regex RangePattern = new(
        @"^Range:\s*bytes=(?<start>[0-9]+)-(?<end>[0-9]*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _loop;

    public byte[] Content { get; }
    public bool ServeNotFound { get; set; }
    public int ChunkDelayMilliseconds { get; set; }
    public Uri BaseUri { get; }

    public StubHttpServer(byte[] content)
    {
        Content = content;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _loop = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleAsync(connection));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
        }
    }

    private async Task HandleAsync(TcpClient connection)
    {
        using (connection)
        {
            try
            {
                var stream = connection.GetStream();
                var request = await ReadRequestAsync(stream);
                if (request is null)
                {
                    return;
                }

                if (ServeNotFound)
                {
                    await WriteRawAsync(
                        stream,
                        "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    );
                    return;
                }

                var (method, range) = request.Value;
                if (string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    var header =
                        $"HTTP/1.1 200 OK\r\nContent-Length: {Content.Length}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n";
                    await WriteRawAsync(stream, header);
                    return;
                }

                if (range is { } rangeGroup)
                {
                    var (start, endInclusive) = rangeGroup;
                    if (endInclusive < 0)
                    {
                        endInclusive = Content.Length - 1;
                    }
                    var length = (int)(endInclusive - start + 1);
                    var header =
                        $"HTTP/1.1 206 Partial Content\r\nContent-Length: {length}\r\nAccept-Ranges: bytes\r\nContent-Range: bytes {start}-{endInclusive}/{Content.Length}\r\nConnection: close\r\n\r\n";
                    await WriteRawAsync(stream, header);
                    await WriteBodyAsync(stream, (int)start, length);
                    return;
                }

                await WriteRawAsync(
                    stream,
                    $"HTTP/1.1 200 OK\r\nContent-Length: {Content.Length}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n"
                );
                await WriteBodyAsync(stream, 0, Content.Length);
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task WriteBodyAsync(NetworkStream stream, int start, int length)
    {
        const int sliceSize = 32 * 1024;
        var offset = start;
        var remaining = length;
        while (remaining > 0)
        {
            var slice = Math.Min(sliceSize, remaining);
            await stream.WriteAsync(Content.AsMemory(offset, slice));
            await stream.FlushAsync();
            offset += slice;
            remaining -= slice;
            if (ChunkDelayMilliseconds > 0)
            {
                await Task.Delay(ChunkDelayMilliseconds);
            }
        }
    }

    private static async Task<(string Method, (long Start, long End)? Range)?> ReadRequestAsync(
        NetworkStream stream
    )
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(requestLine))
        {
            return null;
        }

        (long Start, long End)? range = null;
        string? headerLine;
        while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
        {
            var match = RangePattern.Match(headerLine);
            if (match.Success)
            {
                var rangeStart = long.Parse(match.Groups["start"].ValueSpan);
                var endText = match.Groups["end"].Value;
                var rangeEnd = endText.Length > 0 ? long.Parse(endText) : -1L;
                range = (rangeStart, rangeEnd);
            }
        }

        var method = requestLine.Split(' ')[0];
        return (method, range);
    }

    private static Task WriteRawAsync(NetworkStream stream, string text)
    {
        return stream.WriteAsync(Encoding.ASCII.GetBytes(text)).AsTask();
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener.Stop();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { }
        _shutdown.Dispose();
    }
}
