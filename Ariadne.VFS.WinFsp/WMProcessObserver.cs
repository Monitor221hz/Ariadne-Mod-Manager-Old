using System.Diagnostics.CodeAnalysis;
using System.Management;

namespace Ariadne.VFS.WinFsp;

public readonly record struct TrackedProcess(string ImagePath, int ParentId);

public sealed class WMProcessObserver
{
    private readonly Dictionary<int, TrackedProcess> _processes = new();
    private readonly ManagementEventWatcher _started;
    private readonly ManagementEventWatcher _stopped;

    public bool TrackingAvailable { get; }

    public event Action<int, string>? ProcessStarted;

    public event Action<int>? ProcessStopped;

    public void Register(int pid, string imagePath, int parentPid)
    {
        lock (_processes)
        {
            _processes[pid] = new TrackedProcess(imagePath, parentPid);
        }
        ProcessStarted?.Invoke(pid, imagePath);
    }

    public void Unregister(int pid)
    {
        bool removed;
        lock (_processes)
        {
            removed = _processes.Remove(pid);
        }
        if (removed)
        {
            ProcessStopped?.Invoke(pid);
        }
    }

    public IReadOnlyDictionary<int, TrackedProcess> Snapshot()
    {
        lock (_processes)
        {
            return new Dictionary<int, TrackedProcess>(_processes);
        }
    }

    public bool TryGetRecord(int pid, [NotNullWhen(true)] out TrackedProcess record)
    {
        lock (_processes)
        {
            return _processes.TryGetValue(pid, out record);
        }
    }

    public bool TryGetImagePath(int pid, out string imagePath)
    {
        if (TryGetRecord(pid, out var record))
        {
            imagePath = record.ImagePath;
            return true;
        }
        imagePath = string.Empty;
        return false;
    }

    public WMProcessObserver()
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
            TrackingAvailable = true;
        }
        catch (ManagementException)
        {
            _started = null!;
            _stopped = null!;
        }
    }

    internal WMProcessObserver(bool trackingAvailable)
    {
        TrackingAvailable = trackingAvailable;
        _started = null!;
        _stopped = null!;
    }

    private void SeedCurrent()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ExecutablePath, ParentProcessId FROM Win32_Process"
        );
        lock (_processes)
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["ExecutablePath"] is string path)
                {
                    int pid = Convert.ToInt32(mo["ProcessId"]);
                    int parentPid = Convert.ToInt32(mo["ParentProcessId"]);
                    _processes[pid] = new TrackedProcess(path, parentPid);
                }
            }
        }
    }

    private void OnStarted(object sender, EventArrivedEventArgs e)
    {
        int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        int parentPid = Convert.ToInt32(e.NewEvent["ParentProcessID"]);
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
            //processes may refuse
        }
        if (path != null)
        {
            lock (_processes)
            {
                _processes[pid] = new TrackedProcess(path, parentPid);
            }
            ProcessStarted?.Invoke(pid, path);
        }
    }

    private void OnStopped(object sender, EventArrivedEventArgs e)
    {
        int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        bool removed;
        lock (_processes)
        {
            removed = _processes.Remove(pid);
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
