using System.Collections.Concurrent;
using Ariadne.WebProtocol;

namespace Ariadne.ModManager.GUI;

public sealed class WebLinkBuffer
{
    private readonly ConcurrentQueue<ISchemeLink> _pending = new();

    public event EventHandler<ISchemeLink>? LinkEnqueued;

    public int Count => _pending.Count;

    public void Enqueue(ISchemeLink link)
    {
        _pending.Enqueue(link);
        LinkEnqueued?.Invoke(this, link);
    }

    public bool TryDequeue(out ISchemeLink? link)
    {
        return _pending.TryDequeue(out link);
    }
}
