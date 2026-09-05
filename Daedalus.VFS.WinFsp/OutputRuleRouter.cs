namespace Daedalus.VFS.WinFsp;

public sealed class OutputRuleRouter : IDisposable
{
    private readonly Dictionary<string, string> _rules;
    private readonly Dictionary<int, string> _hit = new();
    private readonly WMProcessObserver _processes;
    private readonly string _physicalMountRoot;
    private readonly VirtualNode<BackedEntry>? _root;
    private readonly object _hitLock = new();

    public OutputRuleRouter(
        IReadOnlyList<OutputRule> rules,
        WMProcessObserver processes,
        VirtualNode<BackedEntry>? root = null,
        string? physicalMountRoot = null
    )
    {
        _rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            _rules[Path.GetFullPath(rule.Image)] = rule.OutputDirectory;
        }
        _processes = processes;
        _root = root;
        _physicalMountRoot =
            physicalMountRoot is null || physicalMountRoot.Length == 0
                ? ""
                : Path.GetFullPath(physicalMountRoot).TrimEnd('\\', '/')
                    + Path.DirectorySeparatorChar;

        foreach (var (pid, record) in _processes.Snapshot())
        {
            Router_ProcessStarted(pid, record.ImagePath);
        }
        _processes.ProcessStarted += Router_ProcessStarted;
        _processes.ProcessStopped += pid =>
        {
            lock (_hitLock)
            {
                _hit.Remove(pid);
            }
        };
    }

    public bool TryResolve(int pid, out string outputDir) => _hit.TryGetValue(pid, out outputDir!);

    private void Router_ProcessStarted(int pid, string imagePath)
    {
        if (
            _physicalMountRoot.Length > 0
            && _root != null
            && imagePath.StartsWith(_physicalMountRoot, StringComparison.OrdinalIgnoreCase)
        )
        {
            string virtualRemainder = imagePath[_physicalMountRoot.Length..];
            var node = _root.FindNode(virtualRemainder);
            if (node?.Data.PhysicalPath != null)
            {
                imagePath = node.Data.PhysicalPath;
            }
        }
        if (_rules.TryGetValue(imagePath, out var outputDir))
        {
            lock (_hitLock)
            {
                _hit[pid] = outputDir;
            }
            return;
        }

        var visited = new HashSet<int>();
        var cursor = pid;
        while (_processes.TryGetRecord(cursor, out var record))
        {
            if (!visited.Add(cursor))
            {
                break;
            }
            cursor = record.ParentId;
            if (cursor == 0)
            {
                break;
            }
            if (_hit.TryGetValue(cursor, out var inherited))
            {
                lock (_hitLock)
                {
                    _hit[pid] = inherited;
                }
                return;
            }
        }
    }

    public void Dispose()
    {
        _processes.ProcessStarted -= Router_ProcessStarted;
    }
}
