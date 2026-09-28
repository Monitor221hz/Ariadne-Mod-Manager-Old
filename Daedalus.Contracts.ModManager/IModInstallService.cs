namespace Daedalus.Contracts.ModManager;

public sealed record InstallProgress(
    FileInfo Archive,
    string EntryPath,
    long BytesTransferred,
    long? TotalBytes,
    double? ProgressPercentage
);

public interface IModInstallService
{
    event EventHandler<InstallProgress>? InstallProgressChanged;

    Task<ILibraryMod?> InstallAsync(
        string name,
        string? version,
        FileInfo archive,
        ModID? provenanceId = null,
        CancellationToken cancellationToken = default
    );
}
