using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.ModManager;

public sealed class VirtualDeploymentMethod : IModDeploymentMethod
{
    private readonly IVirtualFileSystemFactory _vfsFactory;
    private readonly IDeploymentPaths _paths;
    private Stack<IVirtualFileSystem> _vfsStack = new Stack<IVirtualFileSystem>();
    private List<OutputRule> _outputRules = new List<OutputRule>();
    public ModDeploymentFlags Flags => ModDeploymentFlags.EmptyMountPoints;

    private Dictionary<IGamePath, DirectoryInfo> _gamePathDirectoryMap;

    public VirtualDeploymentMethod(IVirtualFileSystemFactory vfsFactory, IDeploymentPaths paths)
    {
        _vfsFactory = vfsFactory;
        _paths = paths;
        _gamePathDirectoryMap = new();
    }

    private void DeployPath(
        IInstalledGame game,
        IGamePath deploymentPath,
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap
    )
    {
        var vfs = _vfsFactory.Create();
        var sourcePath = game.LookupAbsolutePath(deploymentPath);
        var virtualRoot = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        virtualRoot.LinkDirectory(sourcePath, "");
        virtualRoot.LinkDirectory(
            _paths.OverwriteDirectory.FullName,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        foreach (var rule in _outputRules)
        {
            Directory.CreateDirectory(rule.OutputDirectory);
            virtualRoot.LinkDirectory(
                rule.OutputDirectory,
                "",
                LinkFlags.Recursive | LinkFlags.Whiteouts
            );
        }
        virtualRootKeyMap.Add(deploymentPath.Key, virtualRoot);
        var mountDirectory = new DirectoryInfo(
            Path.Join(_paths.StagingDirectory.FullName, deploymentPath.Key)
        );
        if (mountDirectory.Exists)
        {
            mountDirectory.Delete(true);
        }
        mountDirectory.Create();
        var settings = new VirtualFileSystemSettings(true, mountDirectory, _outputRules);
        vfs.Mount(virtualRoot, settings);
        _vfsStack.Push(vfs);

        _gamePathDirectoryMap.Add(deploymentPath, mountDirectory);
        // deploymentPath.DeployedDirectory = mountDirectory;
    }

    private static bool TryGetVirtualRoot(
        IGamePath gamePath,
        ISupportedGame configuration,
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap,
        [NotNullWhen(true)] out VirtualNode<BackedEntry>? virtualRoot
    )
    {
        var current = gamePath;
        HashSet<string> visitedKeys = new(StringComparer.OrdinalIgnoreCase) { current.Key };
        while (!virtualRootKeyMap.TryGetValue(current.Key, out virtualRoot))
        {
            if (
                current.BasedOn == null
                || !visitedKeys.Add(current.BasedOn)
                || !configuration.TryGetValue(current.BasedOn, out var parent)
            )
            {
                return false;
            }
            current = parent;
        }
        return true;
    }

    public bool TryGetDeployedPath(
        IInstalledGame game,
        IGamePath path,
        out DirectoryInfo? directoryInfo
    )
    {
        return _gamePathDirectoryMap.TryGetValue(path, out directoryInfo);
    }

    public void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods)
    {
        var modOrder = mods.OrderBy(m => m.Priority);
        var configuration = game.Configuration;
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap = new();
        DeployPath(game, configuration.Root, virtualRootKeyMap);
        foreach (var deploymentPath in configuration.Deployments)
        {
            DeployPath(game, deploymentPath, virtualRootKeyMap);
        }
        foreach (var mod in modOrder)
        {
            mod.Directory.Refresh();
            if (!mod.Directory.Exists || !configuration.TryGetValue(mod.Target, out var gamePath))
            {
                continue;
            }
            if (!TryGetVirtualRoot(gamePath, configuration, virtualRootKeyMap, out var virtualRoot))
            {
                continue;
            }

            foreach (
                var fileSysInfo in mod.Directory.EnumerateFileSystemInfos(
                    "*",
                    SearchOption.TopDirectoryOnly
                )
            )
            {
                var relativePath = Path.GetRelativePath(
                    mod.Directory.FullName,
                    fileSysInfo.FullName
                );
                var fullPath = Path.Join(game.LookupAbsolutePath(gamePath), relativePath);
                switch (fileSysInfo)
                {
                    case FileInfo file:
                        virtualRoot.LinkFile(file.FullName, fullPath);
                        break;
                    case DirectoryInfo dir:
                        virtualRoot.LinkDirectory(
                            dir.FullName,
                            fullPath,
                            LinkFlags.Recursive | LinkFlags.Whiteouts
                        );
                        break;
                }
            }
        }
    }

    public void Dispose()
    {
        while (_vfsStack.Count > 0)
        {
            var vfs = _vfsStack.Pop();
            vfs.Dispose();
        }
    }

    public void Revert(IInstalledGame game)
    {
        foreach (var vfs in _vfsStack)
        {
            vfs.Unmount();
        }
    }
}
