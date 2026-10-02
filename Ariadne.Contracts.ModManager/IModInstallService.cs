using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public sealed record InstallProgress(FileInfo Archive, string EntryPath);

public interface IModInstallService
{
    event EventHandler<InstallProgress>? InstallProgressChanged;

    Task<ILibraryMod?> InstallAsync(
        string name,
        string? version,
        FileInfo archive,
        ModID? provenanceId = null,
        InstallType installType = InstallType.New,
        IGamePath? target = null,
        CancellationToken cancellationToken = default
    );

    Task<ILibraryMod?> InstallDownloadAsync(
        FileInfo archive,
        IGamePath? target = null,
        CancellationToken cancellationToken = default
    );
}
