using ByteSizeLib;
using Daedalus.Contracts.Mods;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DeployedRowViewModel : ViewModelBase
{
    private string? _sizeText;
    private string? _lastModifiedText;

    public string Name { get; }
    public string Origin { get; }
    public long SizeBytes { get; }
    public string SizeText => _sizeText ??= ByteSize.FromBytes(SizeBytes).ToString();
    public string LastModifiedText => _lastModifiedText ??= ComputeLastModified();
    public bool IsDirectory { get; }
    public bool IsArchive { get; }
    public bool IsLeaf => Children.Count == 0;
    public bool ShowsIcon => !IsDirectory && !IsArchive;
    public IReadOnlyList<DeployedRowViewModel> Children { get; }

    private readonly DateTimeOffset _lastModifiedUtc;

    public DeployedRowViewModel(
        string name,
        ModFileEntry entry,
        string origin,
        IReadOnlyList<DeployedRowViewModel> children
    )
    {
        Name = name;
        Origin = origin;
        IsDirectory = entry.Kind == ModEntryKind.Directory;
        IsArchive = entry.Kind == ModEntryKind.Archive;
        Children = children;
        _lastModifiedUtc = entry.LastModifiedUtc;
        SizeBytes = IsDirectory ? children.Sum(c => c.SizeBytes) : entry.Size;
    }

    private string ComputeLastModified() =>
        IsDirectory || _lastModifiedUtc == DateTimeOffset.MinValue
            ? ""
            : _lastModifiedUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
