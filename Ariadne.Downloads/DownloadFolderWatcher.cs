namespace Ariadne.Downloads;

public static class DownloadArchiveFilter
{
    public static bool IsModArchive(FileInfo file)
    {
        return file.Extension switch
        {
            { } extension when extension.Equals(".download", StringComparison.OrdinalIgnoreCase) =>
                false,
            { } extension when extension.Equals(".json", StringComparison.OrdinalIgnoreCase) =>
                false,
            _ => true,
        };
    }
}

public interface IDownloadFolderWatcher : IDisposable
{
    event EventHandler<string>? PathChanged;

    IReadOnlyList<FileInfo> ScanArchives();
}

public sealed class DownloadFolderWatcher : IDownloadFolderWatcher
{
    private readonly DirectoryInfo _folder;
    private readonly FileSystemWatcher _watcher;

    public event EventHandler<string>? PathChanged;

    public DownloadFolderWatcher(DirectoryInfo folder)
    {
        _folder = folder;
        folder.Refresh();
        if (!folder.Exists)
        {
            folder.Create();
        }
        _watcher = new FileSystemWatcher(folder.FullName)
        {
            NotifyFilter =
                NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, args) => PathChanged?.Invoke(this, args.FullPath);
        _watcher.Deleted += (_, args) => PathChanged?.Invoke(this, args.FullPath);
        _watcher.Renamed += (_, args) =>
        {
            PathChanged?.Invoke(this, args.OldFullPath);
            PathChanged?.Invoke(this, args.FullPath);
        };
    }

    public IReadOnlyList<FileInfo> ScanArchives()
    {
        _folder.Refresh();
        if (!_folder.Exists)
        {
            _folder.Create();
            return [];
        }
        return _folder.EnumerateFiles().Where(DownloadArchiveFilter.IsModArchive).ToList();
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
