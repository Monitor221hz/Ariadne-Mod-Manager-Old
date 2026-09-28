namespace Daedalus.Contracts.ModManager;

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
        CancellationToken cancellationToken = default
    );
}
