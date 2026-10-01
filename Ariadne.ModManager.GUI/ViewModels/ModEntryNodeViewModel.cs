using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using ByteSizeLib;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : TreeNodeViewModel
{
    private readonly IModListEntry _entry;
    private readonly Subject<Unit> _removeRequested = new();
    private readonly Subject<Unit> _removeFromProfileRequested = new();
    private uint _priorityValue;
    private bool _active;
    private SelectedModVerdict? _conflictVerdict;
    private readonly Subject<CancellationToken> _beginLoad = new();
    private readonly Subject<Unit> _targetChanged = new();
    private readonly ObservableAsPropertyHelper<VirtualNode<ModFileEntry>?> _content;
    private readonly ObservableAsPropertyHelper<string> _sizeText;
    private string _displayName;
    public IModListEntry Entry => _entry;
    public ILibraryMod Model => _entry.Mod;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _entry.Mod.Info.Version;
    public bool Active
    {
        get => _active;
        set
        {
            _entry.Active = value;
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
        ContentTree?.Children.Select(child => ContentNodeViewModel.Wrap(child, this))
        ?? Enumerable.Empty<TreeNodeViewModel>();
    public override bool HasChildren => ContentTree is { Children.Count: > 0 };

    public void ConnectContentTree(CancellationToken ct) => _beginLoad.OnNext(ct);

    public void ReloadContent()
    {
        _entry.Mod.RefreshContent();
        _beginLoad.OnNext(CancellationToken.None);
    }

    public ModEntryNodeViewModel(IModListEntry entry)
    {
        var mod = entry.Mod;
        _entry = entry;
        _displayName = mod.Name;
        _active = entry.Active;
        DeleteFromDiskCommand = ReactiveCommand.Create(() => _removeRequested.OnNext(Unit.Default));
        ForgetFromProfileCommand = ReactiveCommand.Create(() =>
            _removeFromProfileRequested.OnNext(Unit.Default)
        );
        SetTargetCommand = ReactiveCommand.Create<IGamePath>(target =>
        {
            mod.Info.Target = target.Key;
            this.RaisePropertyChanged(nameof(Target));
            _targetChanged.OnNext(Unit.Default);
        });

        var content = _beginLoad
            .Select(ct => Observable.FromAsync(t2 => Task.Run(() => mod.Content, t2)))
            .Switch()
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
    public ReactiveCommand<Unit, Unit> DeleteFromDiskCommand { get; }

    public IObservable<Unit> RemoveFromProfileRequested => _removeFromProfileRequested;
    public ReactiveCommand<Unit, Unit> ForgetFromProfileCommand { get; }

    public string Target => _entry.Mod.Info.Target;
    public IObservable<Unit> TargetChanged => _targetChanged;
    public ReactiveCommand<IGamePath, Unit> SetTargetCommand { get; }

    internal void SetPriorityDisplay(uint priority)
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, priority, nameof(PriorityValue));
    }

    protected override string ApplyRename(string name)
    {
        _entry.Mod.RenameTo(name);
        return _entry.Mod.Name;
    }
}
