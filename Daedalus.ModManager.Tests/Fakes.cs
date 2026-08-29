using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.VFS;

namespace Daedalus.ModManager.Tests;

public sealed class FakeVirtualFileSystem : IVirtualFileSystem
{
    public VirtualNode<BackedEntry>? MountedRoot { get; private set; }
    public VirtualFileSystemSettings? Settings { get; private set; }
    public bool IsUnmounted { get; private set; }
    public bool IsDisposed { get; private set; }

    public void Mount(VirtualNode<BackedEntry> root, VirtualFileSystemSettings settings)
    {
        MountedRoot = root;
        Settings = settings;
    }

    public void Unmount() => IsUnmounted = true;

    public void Dispose() => IsDisposed = true;
}

public sealed class FakeVirtualFileSystemFactory : IVirtualFileSystemFactory
{
    public List<FakeVirtualFileSystem> Created { get; } = new();

    public IVirtualFileSystem Create()
    {
        var vfs = new FakeVirtualFileSystem();
        Created.Add(vfs);
        return vfs;
    }
}

public sealed class TestDeploymentPaths(string overwriteDir, string stagingDir) : IDeploymentPaths
{
    public DirectoryInfo OverwriteDirectory { get; } = new(overwriteDir);
    public DirectoryInfo StagingDirectory { get; } = new(stagingDir);
}

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DaedalusTests-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}
