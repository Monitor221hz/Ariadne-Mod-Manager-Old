using System.Management;

namespace Daedalus.WinFsp;

public sealed class ProcessTracker : IDisposable
{
    private readonly Dictionary<int, string> _imagePaths = new();
    private readonly ManagementEventWatcher _started;
    private readonly ManagementEventWatcher _stopped;

    public event Action<int, string>? ProcessStarted;

    public event Action<int>? ProcessStopped;

    public IReadOnlyDictionary<int, string> Snapshot()
    {
        lock (_imagePaths)
        {
            return new Dictionary<int, string>(_imagePaths);
        }
    }

    public bool TryGetImagePath(int pid, out string imagePath)
    {
        lock (_imagePaths)
        {
            return _imagePaths.TryGetValue(pid, out imagePath!);
        }
    }

    public ProcessTracker()
    {
        try
        {
            SeedCurrent();
            _started = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace")
            );
            _stopped = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace")
            );
            _started.EventArrived += OnStarted;
            _stopped.EventArrived += OnStopped;
            _started.Start();
            _stopped.Start();
        }
        catch (ManagementException)
        {
            _started = null!;
            _stopped = null!;
        }
    }

    private void SeedCurrent()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ExecutablePath FROM Win32_Process"
        );
        lock (_imagePaths)
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["ExecutablePath"] is string path)
                {
                    _imagePaths[Convert.ToInt32(mo["ProcessId"])] = path;
                }
            }
        }
    }

    private void OnStarted(object sender, EventArrivedEventArgs e)
    {
        int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        string? path = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {pid}"
            );
            foreach (ManagementObject mo in searcher.Get())
            {
                path = mo["ExecutablePath"] as string;
            }
        }
        catch
        {
            /* system processes may refuse the query */
        }
        if (path != null)
        {
            lock (_imagePaths)
            {
                _imagePaths[pid] = path;
            }
            ProcessStarted?.Invoke(pid, path);
        }
    }

    private void OnStopped(object sender, EventArrivedEventArgs e)
    {
        int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        bool removed;
        lock (_imagePaths)
        {
            removed = _imagePaths.Remove(pid);
        }
        if (removed)
        {
            ProcessStopped?.Invoke(pid);
        }
    }

    public void Dispose()
    {
        _started?.Stop();
        _started?.Dispose();
        _stopped?.Stop();
        _stopped?.Dispose();
    }
}
