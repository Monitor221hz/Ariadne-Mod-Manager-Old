using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class ModInstallService : IModInstallService
{
    private readonly IArchiveExtractor _extractor;
    private readonly IReadOnlyList<IModInstaller> _installers;
    private readonly IModTargeter _targeter;
    private readonly IInstanceService _instances;
    private readonly IModManagerPaths _paths;
    private readonly ILibraryModSerializer _serializer;

    public event EventHandler<InstallProgress>? InstallProgressChanged;

    public ModInstallService(
        IArchiveExtractor extractor,
        IEnumerable<IModInstaller> installers,
        IModTargeter targeter,
        IInstanceService instances,
        IModManagerPaths paths,
        ILibraryModSerializer serializer
    )
    {
        _extractor = extractor;
        _installers = installers.ToList();
        _targeter = targeter;
        _instances = instances;
        _paths = paths;
        _serializer = serializer;

        _extractor.OnExtractionProgress += (sender, args) =>
        {
            if (sender is FileInfo archive)
            {
                InstallProgressChanged?.Invoke(this, new InstallProgress(archive, args.EntryPath));
            }
        };
    }

    public async Task<ILibraryMod?> InstallAsync(
        string name,
        string? version,
        FileInfo archive,
        ModID? provenanceId = null,
        InstallType installType = InstallType.New,
        CancellationToken cancellationToken = default
    )
    {
        if (!_instances.TryGetInstanceGame(out var game))
        {
            return null;
        }

        var extractionRoot = new DirectoryInfo(
            Path.Join(_paths.TemporaryFolder.FullName, $"install-{Guid.NewGuid():N}")
        );
        try
        {
            try
            {
                extractionRoot.Create();
                await Task.Run(
                    () => _extractor.Extract(extractionRoot, archive),
                    cancellationToken
                );
            }
            catch (IOException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            foreach (var installer in _installers)
            {
                if (!installer.CanInstall(game.Configuration, extractionRoot))
                {
                    continue;
                }

                var info = new ModInfo(
                    provenanceId ?? new ModID(0, SourceType.Local),
                    version ?? "",
                    [],
                    "",
                    0,
                    false
                );

                if (installer.TryInstall(name, info, extractionRoot, out var mod, installType))
                {
                    _targeter.ApplyAliases(game.Configuration, mod);
                    mod.Info.Target = _targeter.GetTarget(game.Configuration, mod).Key;
                    _serializer.Save(mod);
                    return mod;
                }
            }

            return null;
        }
        finally
        {
            try
            {
                extractionRoot.Refresh();
                if (extractionRoot.Exists)
                {
                    extractionRoot.Delete(true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
