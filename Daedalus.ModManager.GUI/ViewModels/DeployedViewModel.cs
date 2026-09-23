using System.Reactive;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using CP.Reactive.Collections;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DeployedViewModel : ViewModelBase, IWorkspaceTab
{
    private sealed record DeployedTargetBucket(
        string Display,
        IGamePath? Path,
        List<ILibraryMod> Mods
    );

    private readonly IModProfile _profile;
    private readonly IInstanceService _instances;
    private Task? _initTask;
    private string _summaryTitle = "";
    private string? _selectedTarget;
    private IReadOnlyList<DeployedTargetBucket> _buckets = [];
    private IReadOnlyList<DeployedRowViewModel> _rows = [];

    public string Title => "Deployed";

    public string SummaryTitle
    {
        get => _summaryTitle;
        private set => this.RaiseAndSetIfChanged(ref _summaryTitle, value);
    }

    public HierarchicalModel<DeployedRowViewModel> Model { get; } =
        new(
            new HierarchicalOptions<DeployedRowViewModel>
            {
                ChildrenSelector = row => row.Children,
                IsLeafSelector = row => row.IsLeaf,
                VirtualizeChildren = true,
            }
        );

    public ISortingModel SortingModel { get; } =
        new SortingModel { CycleMode = SortCycleMode.AscendingDescendingNone };

    private List<string> _targetRoots = [];

    public List<string> TargetRoots
    {
        get => _targetRoots;
        private set
        {
            this.RaiseAndSetIfChanged(ref _targetRoots, value);
            this.RaisePropertyChanged(nameof(TargetPickerVisible));
        }
    }
    public bool TargetPickerVisible => TargetRoots.Count > 1;

    public string? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTarget, value);
            RebuildRows();
        }
    }

    public IReadOnlyList<DeployedRowViewModel> Rows
    {
        get => _rows;
        private set
        {
            this.RaiseAndSetIfChanged(ref _rows, value);
            Model.SetRoots(
                new System.Collections.ObjectModel.ObservableCollection<DeployedRowViewModel>(Rows)
            );
        }
    }

    public DeployedViewModel(IModProfile profile, IInstanceService instances)
    {
        _profile = profile;
        _instances = instances;
    }

    public Task EnsureInitializedAsync() => _initTask ??= InitializeAsync();

    public Task RefreshAsync()
    {
        if (_initTask is null)
        {
            return Task.CompletedTask;
        }
        Configure(initialized: false);
        return Task.CompletedTask;
    }

    private Task InitializeAsync()
    {
        Configure(initialized: true);
        return Task.CompletedTask;
    }

    private void Configure(bool initialized)
    {
        var game = _instances.Current?.Game;
        var config = game?.Configuration;
        var gameName = config?.Name ?? "Game";
        var mods = _profile.ModList.Where(m => m.Info.Active).ToList();

        var groupedRaw = mods.GroupBy(
                m => (m.Info.Target ?? "").Trim('\\', '/'),
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        var buckets = groupedRaw
            .Select(group => new DeployedTargetBucket(
                ResolveDisplayKey(group.Key, config),
                ResolveGamePath(group.Key, config),
                group.ToList()
            ))
            .ToList();

        SummaryTitle =
            buckets.Count == 1 && buckets[0].Path is { } p ? $"{gameName}/{p.Key}" : gameName;

        TargetRoots = buckets.Select(b => b.Display).ToList();
        _buckets = buckets;

        var stillExists = buckets.FirstOrDefault(b => b.Display == _selectedTarget);
        if (initialized || stillExists is null)
        {
            var preferred = buckets.FirstOrDefault(b =>
                b.Path is { } pp
                && config?.InstallTargets.Any(t =>
                    t.Key.Equals(pp.Key, StringComparison.OrdinalIgnoreCase)
                ) == true
            );
            _selectedTarget = preferred?.Display ?? (TargetRoots.Count > 0 ? TargetRoots[0] : null);
        }
        RebuildRows();
    }

    private static string ResolveDisplayKey(string rawTarget, ISupportedGame? config)
    {
        if (string.IsNullOrEmpty(rawTarget))
        {
            return config?.Root.Key ?? "root";
        }
        return
            config?.InstallTargets.FirstOrDefault(t =>
                t.Key.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
                || t.DirectoryPath.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
            )
                is { } targ
            ? targ.Key
            : $"<unresolved: {rawTarget}>";
    }

    private static IGamePath? ResolveGamePath(string rawTarget, ISupportedGame? config)
    {
        if (string.IsNullOrEmpty(rawTarget))
        {
            return config?.Root;
        }
        return config?.InstallTargets.FirstOrDefault(t =>
            t.Key.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
            || t.DirectoryPath.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
        );
    }

    private void RebuildRows()
    {
        var selected = _buckets.FirstOrDefault(b => b.Display == _selectedTarget);
        if (selected is null)
        {
            Rows = [];
            return;
        }
        Rows = ComputeRows(selected);
    }

    private List<DeployedRowViewModel> ComputeRows(DeployedTargetBucket bucket)
    {
        var mods = bucket.Mods.OrderBy(m => m.Info.Priority).ToList();

        var merged = new VirtualNode<ModFileEntry>("", NodeFlags.Directory, null, null);
        var namesByInfo = mods.ToDictionary(m => m.Info, m => m.Name);

        foreach (var mod in mods)
        {
            var content = mod.Content;
            var stack = new Stack<(VirtualNode<ModFileEntry> Node, string Prefix)>();
            stack.Push((content, ""));
            while (stack.Count > 0)
            {
                var (node, prefix) = stack.Pop();
                foreach (var child in node.Children)
                {
                    var relative = prefix.Length == 0 ? child.Name : prefix + "\\" + child.Name;
                    if (child.Data is not null)
                    {
                        merged.AddFile(
                            relative,
                            child.Data,
                            child.IsDirectory ? NodeFlags.Directory : NodeFlags.None
                        );
                    }
                    stack.Push((child, relative));
                }
            }
        }

        return BuildRows(merged, namesByInfo);
    }

    private static List<DeployedRowViewModel> BuildRows(
        VirtualNode<ModFileEntry> node,
        IReadOnlyDictionary<IModInfo, string> namesByInfo
    )
    {
        var result = new List<DeployedRowViewModel>(node.Count);
        foreach (var child in node.Children)
        {
            var children = child.IsDirectory ? BuildRows(child, namesByInfo) : [];
            var entry = child.Data is not null ? child.Data : CreateDirectoryEntry(child.Name);
            var origin =
                entry.Origin is not null && namesByInfo.TryGetValue(entry.Origin, out var who)
                    ? who
                    : "";
            result.Add(new DeployedRowViewModel(child.Name, entry, origin, children));
        }
        return result;
    }

    private static ModFileEntry CreateDirectoryEntry(string name) =>
        new(name, ModEntryKind.Directory, DirectoryOrigin.Value, "", 0, DateTimeOffset.MinValue);
}

file static class DirectoryOrigin
{
    public static readonly IModInfo Value = new PlaceholderModInfo();

    private sealed class PlaceholderModInfo : IModInfo
    {
        public ulong ID => 0;
        public SourceType IDSource => SourceType.Local;
        public string Version => "";
        public List<string> Categories => [];
        public string Target { get; set; } = "";
        public uint Priority { get; set; }
        public bool Active { get; set; }
    }
}
