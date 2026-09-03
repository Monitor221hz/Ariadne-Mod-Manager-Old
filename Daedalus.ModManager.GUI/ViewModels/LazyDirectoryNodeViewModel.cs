using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public abstract class DirectoryChildrenNodeViewModel : TreeNodeViewModel
{
    private readonly DirectoryInfo _source;
    private List<TreeNodeViewModel>? _children;

    protected DirectoryChildrenNodeViewModel(DirectoryInfo source)
    {
        _source = source;
    }

    public override IEnumerable<TreeNodeViewModel> Children => _children ??= EnumerateChildren();

    public override bool HasChildren => _children is null || _children.Count > 0;

    private List<TreeNodeViewModel> EnumerateChildren()
    {
        _source.Refresh();
        if (!_source.Exists)
        {
            return [];
        }
        return _source
            .EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(entry => entry is DirectoryInfo)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select<FileSystemInfo, TreeNodeViewModel>(entry =>
                entry is DirectoryInfo dir
                    ? new DirectoryNodeViewModel(dir)
                    : new FileLeafNodeViewModel((FileInfo)entry)
            )
            .ToList();
    }

    public override void Unload()
    {
        _children = null;
        this.RaisePropertyChanged(nameof(HasChildren));
    }
}

public sealed class DirectoryNodeViewModel : DirectoryChildrenNodeViewModel
{
    public override string DisplayName => Source.Name;
    private DirectoryInfo Source { get; }

    public DirectoryNodeViewModel(DirectoryInfo directory)
        : base(directory)
    {
        Source = directory;
    }
}

public sealed class FileLeafNodeViewModel : TreeNodeViewModel
{
    public override string DisplayName { get; }
    public override string SizeText { get; }

    public FileLeafNodeViewModel(FileInfo file)
    {
        DisplayName = file.Name;
        SizeText = FormatSize(file.Length);
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1L << 10 => $"{bytes} B",
            < 1L << 20 => $"{bytes / (1.0 * (1 << 10)):0.#} KB",
            < 1L << 30 => $"{bytes / (1.0 * (1 << 20)):0.#} MB",
            _ => $"{bytes / (1.0 * (1 << 30)):0.#} GB",
        };
}
