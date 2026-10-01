using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ariadne.Contracts.ModManager;
using Avalonia.Media;
using ByteSizeLib;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class GroupHeaderNodeViewModel : TreeNodeViewModel
{
    private readonly ObservableCollection<TreeNodeViewModel> _children;
    private readonly Subject<Unit> _dissolveRequested = new();

    private readonly IDisposable _sizeSubscription;
    private string _sizeText;
    private string _displayName;
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    public IBrush SeparatorBrush { get; }
    public IModGroup Group { get; }
    public override IEnumerable<TreeNodeViewModel> Children => _children;
    public override bool HasChildren => _children.Count > 0;
    public override bool ExpanderVisible => HasChildren;
    public ObservableCollection<TreeNodeViewModel> ObservableChildren => _children;

    public override string SizeText
    {
        get => _sizeText;
    }

    public override long SizeBytes => Children.Sum(c => c.SizeBytes < 0 ? 0 : c.SizeBytes);
    public override bool RenameAllowed => true;

    public void RefreshSizeText() =>
        this.RaiseAndSetIfChanged(
            ref _sizeText,
            ByteSize.FromBytes(SizeBytes).ToString(),
            nameof(SizeText)
        );

    public GroupHeaderNodeViewModel(IModGroup group)
    {
        Group = group;
        _displayName = group.Name;
        var color = group.HeaderColor;
        SeparatorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        _children = new ObservableCollection<TreeNodeViewModel>(
            group.Select(entry => new ModEntryNodeViewModel(entry))
        );
        _children.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasChildren));
            this.RaisePropertyChanged(nameof(ExpanderVisible));
        };
        IsExpanded = HasChildren;
        _sizeText = "↺";
        DissolveCommand = ReactiveCommand.Create(() => _dissolveRequested.OnNext(Unit.Default));

        _sizeSubscription = Observable
            .FromEventPattern<
                NotifyCollectionChangedEventHandler,
                NotifyCollectionChangedEventArgs
            >(
                handler => _children.CollectionChanged += handler,
                handler => _children.CollectionChanged -= handler
            )
            .Select(_ => ByteSize.FromBytes(SizeBytes).ToString())
            .Subscribe(text =>
            {
                _sizeText = text;
                this.RaisePropertyChanged(nameof(SizeText));
            });
    }

    public IObservable<Unit> DissolveRequested => _dissolveRequested;
    public ReactiveCommand<Unit, Unit> DissolveCommand { get; }

    protected override string ApplyRename(string name)
    {
        Group.Name = name;
        return name;
    }
}
