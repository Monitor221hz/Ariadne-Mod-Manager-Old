using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;

namespace Ariadne.WebProtocol;

public sealed class InstanceForwarder : IDisposable
{
    private const string ActivatePayload = "activate";
    private const int MaxPayloadBytes = 8192;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _shutdown = new();
    private readonly FileStream _instanceLock;
    private readonly FileInfo _lockFile;
    private readonly string _pipeName;
    private readonly Task _listener;

    public event EventHandler<PayloadReceivedEventArgs>? PayloadReceived;
    public event EventHandler? ActivationRequested;

    private InstanceForwarder(FileInfo lockFile, FileStream instanceLock, string pipeName)
    {
        _lockFile = lockFile;
        _instanceLock = instanceLock;
        _pipeName = pipeName;
        _listener = Task.Run(ListenAsync);
    }

    public static bool TryCreatePrimary(
        string instanceKey,
        [NotNullWhen(true)] out InstanceForwarder? forwarder
    )
    {
        forwarder = null;
        var scope = ResolveScope(instanceKey);
        var lockFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"Ariadne-{scope}.lock"));

        FileStream instanceLock;
        try
        {
            instanceLock = new FileStream(
                lockFile.FullName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None
            );
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        forwarder = new InstanceForwarder(lockFile, instanceLock, $"Ariadne-{scope}");
        return true;
    }

    public static bool Forward(string instanceKey, LaunchIntent intent)
    {
        var scope = ResolveScope(instanceKey);
        var payload =
            intent.Link is null || intent.Scheme is null
                ? ActivatePayload
                : $"{intent.Scheme} {intent.Link}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                $"Ariadne-{scope}",
                PipeDirection.Out
            );
            client.Connect((int)ConnectTimeout.TotalMilliseconds);
            client.Write(payloadBytes, 0, payloadBytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            return false;
        }
    }

    private async Task ListenAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );
                await server.WaitForConnectionAsync(_shutdown.Token);

                using var message = new MemoryStream();
                var buffer = new byte[4096];
                var oversized = false;
                int read;
                while (
                    (
                        read = await server.ReadAsync(
                            buffer.AsMemory(0, buffer.Length),
                            _shutdown.Token
                        )
                    ) > 0
                )
                {
                    message.Write(buffer, 0, read);
                    if (message.Length > MaxPayloadBytes)
                    {
                        oversized = true;
                        break;
                    }
                }

                if (!oversized)
                {
                    Dispatch(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            { }
        }
    }

    private void Dispatch(string payload)
    {
        try
        {
            if (string.Equals(payload, ActivatePayload, StringComparison.Ordinal))
            {
                ActivationRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var separator = payload.IndexOf(' ');
            if (separator > 0)
            {
                PayloadReceived?.Invoke(
                    this,
                    new PayloadReceivedEventArgs(payload[..separator], payload[(separator + 1)..])
                );
            }
        }
        catch (Exception) { }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _listener.Wait(ShutdownTimeout);
        }
        catch (AggregateException) { }
        _shutdown.Dispose();
        _instanceLock.Dispose();

        try
        {
            _lockFile.Refresh();
            if (_lockFile.Exists)
            {
                _lockFile.Delete();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ResolveScope(string instanceKey)
    {
        var key = Sanitize(instanceKey);
        var user = Sanitize(Environment.UserName, fallback: "user");
        return $"{key}-{user}";
    }

    private static string Sanitize(string value, string? fallback = null)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.')
            {
                builder.Append(character);
            }
        }

        var sanitized = builder.ToString();
        if (sanitized.Length > 0)
        {
            return sanitized;
        }
        if (fallback is not null)
        {
            return fallback;
        }

        throw new ArgumentException(
            "Instance key must contain at least one letter, digit, hyphen, underscore or dot.",
            nameof(value)
        );
    }
}
