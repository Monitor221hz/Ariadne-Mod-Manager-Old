using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using ByteSizeLib;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public abstract class ContentHostNodeViewModel : TreeNodeViewModel
{
    private readonly Subject<CancellationToken> _beginLoad = new();
    private readonly ObservableAsPropertyHelper<VirtualNode<ModFileEntry>?> _content;
    private readonly ObservableAsPropertyHelper<string> _sizeText;

    protected ContentHostNodeViewModel(ILibraryMod mod)
    {
        Model = mod;
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

    public ILibraryMod Model { get; }

    public DirectoryInfo ContentDirectory => Model.Directory;

    public VirtualNode<ModFileEntry>? ContentTree => _content.Value;

    public override long SizeBytes =>
        ContentTree is null ? -1 : ContentTree.SelfAndDescendants().Sum(n => n.Data?.Size ?? 0);

    public override string SizeText => _sizeText.Value;

    public override IEnumerable<TreeNodeViewModel> Children =>
        ContentTree?.Children.Select(child => ContentNodeViewModel.Wrap(child, this))
        ?? Enumerable.Empty<TreeNodeViewModel>();

    public override bool HasChildren => ContentTree is { Children.Count: > 0 };

    public void ConnectContentTree(CancellationToken ct) => _beginLoad.OnNext(ct);

    public void ReloadContent()
    {
        Model.RefreshContent();
        this.RaisePropertyChanged(nameof(VersionText));
        _beginLoad.OnNext(CancellationToken.None);
    }
}
