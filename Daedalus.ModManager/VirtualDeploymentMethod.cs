using Daedalus.Contracts.Games;
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

    public VirtualDeploymentMethod(IVirtualFileSystemFactory vfsFactory, IDeploymentPaths paths)
    {
        _vfsFactory = vfsFactory;
        _paths = paths;
    }

    public void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods)
    {
        var modOrder = mods.OrderBy(m => m.Priority);
        var configuration = game.Configuration;
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap = new();
        foreach (var deploymentPath in configuration.Deployments)
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
            var settings = new VirtualFileSystemSettings(
                true,
                _paths.StagingDirectory,
                _outputRules
            );
            vfs.Mount(virtualRoot, settings);
            _vfsStack.Push(vfs);
            virtualRootKeyMap.Add(deploymentPath.Key, virtualRoot);
        }

        foreach (var mod in modOrder)
        {
            mod.Directory.Refresh();
            if (
                !mod.Directory.Exists
                || !configuration.PathNameMap.TryGetValue(mod.Target, out var gamePath)
            )
            {
                continue;
            }
            VirtualNode<BackedEntry>? virtualRoot = null;
            while (!virtualRootKeyMap.TryGetValue(gamePath.Key, out virtualRoot))
            {
                if (
                    gamePath.BasedOn == null
                    || !configuration.PathNameMap.TryGetValue(
                        gamePath.BasedOn,
                        out var parentGamePath
                    )
                )
                {
                    break;
                }
                gamePath = parentGamePath;
            }
            if (virtualRoot == null)
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
