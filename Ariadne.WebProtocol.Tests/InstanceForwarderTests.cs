using System.Collections.Concurrent;
using Ariadne.WebProtocol;
using Xunit;

namespace Ariadne.WebProtocol.Tests;

public class InstanceForwarderTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private static string NewKey()
    {
        return $"tests-{Guid.NewGuid():N}";
    }

    private static InstanceForwarder CreatePrimary(string key)
    {
        Assert.True(InstanceForwarder.TryCreatePrimary(key, out var forwarder));
        return forwarder;
    }

    [Fact]
    public void TryCreatePrimary_AlreadyHeld_SecondAttemptFails()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);

        var acquired = InstanceForwarder.TryCreatePrimary(key, out var secondary);

        Assert.False(acquired);
        Assert.Null(secondary);
    }

    [Fact]
    public async Task Forward_LinkIntent_RaisesPayloadReceivedWithScheme()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);
        var received = new TaskCompletionSource<PayloadReceivedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        primary.PayloadReceived += (_, payload) => received.TrySetResult(payload);

        var forwarded = InstanceForwarder.Forward(
            key,
            new LaunchIntent { Scheme = "modl", Link = "modl://skyrim/?url=x" }
        );

        Assert.True(forwarded);
        var payload = await received.Task.WaitAsync(EventTimeout);
        Assert.Equal("modl", payload.Scheme);
        Assert.Equal("modl://skyrim/?url=x", payload.Payload);
    }

    [Fact]
    public async Task Forward_EmptyIntent_RaisesActivationRequested()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);
        var activated = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        primary.ActivationRequested += (_, _) => activated.TrySetResult();

        var forwarded = InstanceForwarder.Forward(key, new LaunchIntent());

        Assert.True(forwarded);
        await activated.Task.WaitAsync(EventTimeout);
    }

    [Fact]
    public async Task Forward_SequentialIntents_AllArriveInOrder()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);
        var received = new ConcurrentQueue<string>();
        var allReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        primary.PayloadReceived += (_, payload) =>
        {
            received.Enqueue($"{payload.Scheme}:{payload.Payload}");
            if (received.Count == 3)
            {
                allReceived.TrySetResult();
            }
        };

        var payload = new[] { "nxm:link-a", "modl:link-b", "nxm:link-c" };
        foreach (var item in payload)
        {
            var scheme = item.Split(':', 2);
            Assert.True(
                InstanceForwarder.Forward(
                    key,
                    new LaunchIntent { Scheme = scheme[0], Link = scheme[1] }
                )
            );
        }

        await allReceived.Task.WaitAsync(EventTimeout);
        Assert.Equal(payload, received.ToArray());
    }

    [Fact]
    public void Forward_WithoutPrimary_ReturnsFalse()
    {
        var forwarded = InstanceForwarder.Forward(
            NewKey(),
            new LaunchIntent { Scheme = "nxm", Link = "x" }
        );

        Assert.False(forwarded);
    }

    [Fact]
    public void Dispose_ReleasesInstance_NewPrimaryCanAcquire()
    {
        var key = NewKey();

        CreatePrimary(key).Dispose();

        Assert.True(InstanceForwarder.TryCreatePrimary(key, out var second));
        second.Dispose();
    }

    [Fact]
    public void TryCreatePrimary_UnusableKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => InstanceForwarder.TryCreatePrimary("!!!", out _));
    }
}
