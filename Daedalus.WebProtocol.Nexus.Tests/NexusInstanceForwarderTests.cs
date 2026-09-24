using System.Collections.Concurrent;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusInstanceForwarderTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private static string NewKey()
    {
        return $"tests-{Guid.NewGuid():N}";
    }

    private static NxmLink SampleLink()
    {
        return NxmLink.Parse(
            "nxm://stardewvalley/mods/2400/files/123456?key=abc&expires=1893456000&user_id=42"
        );
    }

    private static NexusInstanceForwarder CreatePrimary(string key)
    {
        Assert.True(NexusInstanceForwarder.TryCreatePrimary(key, out var forwarder));
        return forwarder;
    }

    [Fact]
    public void TryCreatePrimary_AlreadyHeld_SecondAttemptFails()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);

        var acquired = NexusInstanceForwarder.TryCreatePrimary(key, out var secondary);

        Assert.False(acquired);
        Assert.Null(secondary);
    }

    [Fact]
    public async Task Forward_LinkIntent_RaisesLinkReceivedOnPrimary()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);
        var received = new TaskCompletionSource<NxmLink>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        primary.LinkReceived += (_, link) => received.TrySetResult(link);

        var link = SampleLink();
        var forwarded = NexusInstanceForwarder.Forward(key, new NexusLaunchIntent { Link = link });

        Assert.True(forwarded);
        Assert.Equal(link, await received.Task.WaitAsync(EventTimeout));
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

        var forwarded = NexusInstanceForwarder.Forward(key, new NexusLaunchIntent { Link = null });

        Assert.True(forwarded);
        await activated.Task.WaitAsync(EventTimeout);
    }

    [Fact]
    public async Task Forward_SequentialLinks_AllArriveInOrder()
    {
        var key = NewKey();
        using var primary = CreatePrimary(key);
        var received = new ConcurrentQueue<string>();
        var allReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        primary.LinkReceived += (_, link) =>
        {
            received.Enqueue(link.ToString());
            if (received.Count == 3)
            {
                allReceived.TrySetResult();
            }
        };

        var links = new[]
        {
            "nxm://stardewvalley/mods/1001/files/10001?key=a&expires=1893456000&user_id=1",
            "nxm://skyrim/mods/2002/files/20002?key=b&expires=1893456000&user_id=2",
            "nxm://starfield/collections/abc123/revisions/7",
        };
        foreach (var link in links)
        {
            Assert.True(
                NexusInstanceForwarder.Forward(
                    key,
                    new NexusLaunchIntent { Link = NxmLink.Parse(link) }
                )
            );
        }

        await allReceived.Task.WaitAsync(EventTimeout);
        Assert.Equal(links, received.ToArray());
    }

    [Fact]
    public void Forward_WithoutPrimary_ReturnsFalse()
    {
        var forwarded = NexusInstanceForwarder.Forward(
            NewKey(),
            new NexusLaunchIntent { Link = SampleLink() }
        );

        Assert.False(forwarded);
    }

    [Fact]
    public void Dispose_ReleasesInstance_NewPrimaryCanAcquire()
    {
        var key = NewKey();

        CreatePrimary(key).Dispose();

        Assert.True(NexusInstanceForwarder.TryCreatePrimary(key, out var second));
        second.Dispose();
    }

    [Fact]
    public void TryCreatePrimary_UnusableKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            NexusInstanceForwarder.TryCreatePrimary("!!!", out _)
        );
    }
}
