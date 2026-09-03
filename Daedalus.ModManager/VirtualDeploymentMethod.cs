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

    public void SetOutputRules(List<OutputRule> outputRules)
    {
        _outputRules = outputRules;
    }

    private void DeployPath(
        IInstalledGame game,
        IGamePath deploymentPath,
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap,
        bool inPlace
    )
    {
        var vfs = _vfsFactory.Create();
        var sourcePath = game.LookupAbsolutePath(deploymentPath);
        var virtualRoot = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);

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

        DirectoryInfo mountDirectory;
        if (!inPlace)
        {
            mountDirectory = new DirectoryInfo(
                Path.Join(_paths.StagingDirectory.FullName, deploymentPath.Key)
            );
            if (mountDirectory.Exists)
            {
                mountDirectory.Delete(true);
            }
            mountDirectory.Create();
            virtualRoot.LinkDirectory(sourcePath, "");
        }
        else
        {
            mountDirectory = new DirectoryInfo(sourcePath); // responsibility of caller to ensure empty mount dir with WinFsp
        }

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
        [NotNullWhen(true)] out DirectoryInfo? directoryInfo
    )
    {
        return _gamePathDirectoryMap.TryGetValue(path, out directoryInfo);
    }

    public void Deploy(IInstalledGame game, IReadOnlyList<ILibraryMod> mods)
    {
        var modOrder = mods.OrderBy(m => m.Info.Priority);
        var configuration = game.Configuration;
        Dictionary<string, VirtualNode<BackedEntry>> virtualRootKeyMap = new();
        DeployPath(game, configuration.Root, virtualRootKeyMap, false);
        foreach (var deploymentPath in configuration.Deployments)
        {
            DeployPath(game, deploymentPath, virtualRootKeyMap, true);
        }
        foreach (var mod in modOrder)
        {
            mod.Directory.Refresh();
            if (
                !mod.Directory.Exists
                || !configuration.TryGetValue(mod.Info.Target, out var gamePath)
            )
            {
                continue;
            }
            if (!TryGetVirtualRoot(gamePath, configuration, virtualRootKeyMap, out var virtualRoot))
            {
                continue;
            }

            foreach (var content in mod.Content.Children)
            {
                var fullPath = Path.Join(game.LookupAbsolutePath(gamePath), content.Name);
                if (content.IsDirectory)
                {
                    virtualRoot.LinkDirectory(
                        content.Data!.AbsolutePath,
                        fullPath,
                        LinkFlags.Recursive | LinkFlags.Whiteouts
                    );
                }
                else
                {
                    virtualRoot.LinkFile(content.Data!.AbsolutePath, fullPath);
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
