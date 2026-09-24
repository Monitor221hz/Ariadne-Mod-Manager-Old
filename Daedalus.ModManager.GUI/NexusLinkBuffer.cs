using System.Collections.Concurrent;
using Daedalus.WebProtocol.Nexus;

namespace Daedalus.ModManager.GUI;

public sealed class NexusLinkBuffer
{
    private readonly ConcurrentQueue<NxmLink> _pending = new();

    public event EventHandler<NxmLink>? LinkEnqueued;

    public int Count => _pending.Count;

    public void Enqueue(NxmLink link)
    {
        _pending.Enqueue(link);
        LinkEnqueued?.Invoke(this, link);
    }

    public bool TryDequeue(out NxmLink? link)
    {
        return _pending.TryDequeue(out link);
    }
}
