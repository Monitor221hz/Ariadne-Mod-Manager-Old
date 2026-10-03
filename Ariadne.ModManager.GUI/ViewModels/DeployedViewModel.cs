using System.Reactive;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Ariadne.VFS;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using CP.Reactive.Collections;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class DeployedViewModel : ViewModelBase, IWorkspaceTab
{
    private readonly IModProfile _profile;
    private readonly IInstanceService _instances;
    private readonly IDeploymentPreviewService _preview;
    private Task? _initTask;
    private string _summaryTitle = "";
    private string? _selectedTarget;
    private IReadOnlyList<DeploymentTargetGroup> _buckets = [];
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
            Model.SetRoots(
                new System.Collections.ObjectModel.ObservableCollection<DeployedRowViewModel>(value)
            );
            this.RaiseAndSetIfChanged(ref _rows, value);
        }
    }

    public DeployedViewModel(
        IModProfile profile,
        IInstanceService instances,
        IDeploymentPreviewService? preview = null
    )
    {
        _profile = profile;
        _instances = instances;
        _preview = preview ?? new DeploymentPreviewService();
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
        var mods = _profile
            .ModList.Where(entry => entry.Active)
            .Select(entry => entry.Mod)
            .ToList();

        var buckets = _preview.GroupByTarget(mods, config);

        SummaryTitle =
            buckets.Count == 1 && buckets[0].Path is not null
                ? $"{gameName}/{buckets[0].DisplayKey}"
                : gameName;

        TargetRoots = buckets.Select(b => b.DisplayKey).ToList();
        _buckets = buckets;

        var stillExists = buckets.FirstOrDefault(b => b.DisplayKey == _selectedTarget);
        if (initialized || stillExists is null)
        {
            var preferred = buckets.FirstOrDefault(b =>
                b.Path is not null
                && config?.InstallTargets.Any(t =>
                    t.Key.Equals(b.Path.Key, StringComparison.OrdinalIgnoreCase)
                ) == true
            );
            SelectedTarget =
                preferred?.DisplayKey ?? (TargetRoots.Count > 0 ? TargetRoots[0] : null);
        }
        RebuildRows();
    }

    private void RebuildRows()
    {
        var selected = _buckets.FirstOrDefault(b => b.DisplayKey == _selectedTarget);
        if (selected is null)
        {
            Rows = [];
            return;
        }
        Rows = ComputeRows(selected);
    }

    private List<DeployedRowViewModel> ComputeRows(DeploymentTargetGroup bucket)
    {
        var merged = _preview.MergeContent(bucket.Mods);
        var namesByInfo = bucket.Mods.ToDictionary(m => m.Info, m => m.Name);
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
            var entry = child.Data!;
            var origin =
                entry.Origin is not null && namesByInfo.TryGetValue(entry.Origin, out var who)
                    ? who
                    : "";
            result.Add(new DeployedRowViewModel(child.Name, entry, origin, children));
        }
        return result;
    }
}
