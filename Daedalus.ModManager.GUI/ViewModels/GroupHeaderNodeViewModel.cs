using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Subjects;
using Avalonia.Media;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class GroupHeaderNodeViewModel : TreeNodeViewModel
{
    private readonly ObservableCollection<TreeNodeViewModel> _children;
    private readonly Subject<Unit> _dissolveRequested = new();
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
        set => this.RaiseAndSetIfChanged(ref _sizeText, value);
    }

    public override long SizeBytes => Children.Sum(c => c.SizeBytes);
    public override bool RenameAllowed => true;

    public GroupHeaderNodeViewModel(IModGroup group)
    {
        Group = group;
        _displayName = group.Name;
        var color = group.HeaderColor;
        SeparatorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        _children = new ObservableCollection<TreeNodeViewModel>(
            group.Select(mod => new ModEntryNodeViewModel((ILibraryMod)mod))
        );
        _children.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasChildren));
            this.RaisePropertyChanged(nameof(ExpanderVisible));
        };
        IsExpanded = HasChildren;
        _sizeText = DiskSize.Format(SizeBytes);
        DissolveCommand = ReactiveCommand.Create(() => _dissolveRequested.OnNext(Unit.Default));
    }

    public IObservable<Unit> DissolveRequested => _dissolveRequested;
    public ReactiveCommand<Unit, Unit> DissolveCommand { get; }

    protected override string ApplyRename(string name)
    {
        Group.Name = name;
        return name;
    }
}
