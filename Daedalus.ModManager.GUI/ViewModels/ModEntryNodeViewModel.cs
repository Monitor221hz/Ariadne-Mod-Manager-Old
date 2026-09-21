using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ByteSizeLib;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : TreeNodeViewModel
{
    private readonly ILibraryMod _mod;
    private readonly Subject<Unit> _removeRequested = new();
    private uint _priorityValue;
    private bool _active;
    private SelectedModVerdict? _conflictVerdict;
    private readonly Subject<CancellationToken> _beginLoad = new();
    private readonly ObservableAsPropertyHelper<VirtualNode<ModFileEntry>?> _content;
    private readonly ObservableAsPropertyHelper<string> _sizeText;
    private string _displayName;
    public ILibraryMod Model => _mod;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _mod.Info.Version;
    public bool Active
    {
        get => _active;
        set
        {
            _mod.Info.Active = value;
            this.RaiseAndSetIfChanged(ref _active, value);
        }
    }
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public override SelectedModVerdict? ConflictVerdict
    {
        get => _conflictVerdict;
        set => this.RaiseAndSetIfChanged(ref _conflictVerdict, value);
    }
    public override bool RenameAllowed => true;
    public override long SizeBytes =>
        ContentTree is null ? -1 : ContentTree.SelfAndDescendants().Sum(n => n.Data?.Size ?? 0);
    public override string SizeText => _sizeText.Value;
    public VirtualNode<ModFileEntry>? ContentTree => _content.Value;

    public override IEnumerable<TreeNodeViewModel> Children =>
        ContentTree?.Children.Select(ContentNodeViewModel.Wrap)
        ?? Enumerable.Empty<TreeNodeViewModel>();
    public override bool HasChildren => ContentTree is { Children.Count: > 0 };

    public void ConnectContentTree(CancellationToken ct) => _beginLoad.OnNext(ct);

    public ModEntryNodeViewModel(ILibraryMod mod)
    {
        _displayName = mod.Name;
        _mod = mod;
        _priorityValue = mod.Info.Priority;
        _active = mod.Info.Active;
        RemoveCommand = ReactiveCommand.Create(() => _removeRequested.OnNext(Unit.Default));

        var content = _beginLoad
            .Take(1)
            .SelectMany(ct => Observable.FromAsync(t2 => Task.Run(() => _mod.Content, t2)))
            .Catch<VirtualNode<ModFileEntry>, OperationCanceledException>(_ =>
                Observable.Empty<VirtualNode<ModFileEntry>>()
            )
            .ObserveOn(AvaloniaScheduler.Instance)
            .Replay(1)
            .AutoConnect();

        _content = content.ToProperty(this, x => x.ContentTree);
        _sizeText = content
            .Select(tree =>
                ByteSize.FromBytes(tree.SelfAndDescendants().Sum(n => n.Data?.Size ?? 0)).ToString()
            )
            .StartWith("↺")
            .ToProperty(this, x => x.SizeText);

        this.WhenAnyValue(x => x.ContentTree)
            .WhereNotNull()
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(Children));
                this.RaisePropertyChanged(nameof(HasChildren));
                this.RaisePropertyChanged(nameof(SizeBytes));
            });
    }

    public IObservable<Unit> RemoveRequested => _removeRequested;
    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public void RefreshFromModel()
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, _mod.Info.Priority, nameof(PriorityValue));
        this.RaiseAndSetIfChanged(ref _active, _mod.Info.Active, nameof(Active));
    }

    protected override string ApplyRename(string name)
    {
        _mod.RenameTo(name);
        return _mod.Name;
    }
}
