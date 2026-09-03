using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class GroupHeaderNodeViewModel : TreeNodeViewModel
{
    private readonly ObservableCollection<TreeNodeViewModel> _children;

    public override string DisplayName { get; }
    public IBrush SeparatorBrush { get; }
    public IModGroup Group { get; }
    public override IEnumerable<TreeNodeViewModel> Children => _children;
    public override bool HasChildren => _children.Count > 0;
    public ObservableCollection<TreeNodeViewModel> ObservableChildren => _children;

    public GroupHeaderNodeViewModel(IModGroup group)
    {
        Group = group;
        DisplayName = group.Name;
        var color = group.HeaderColor;
        SeparatorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        _children = new ObservableCollection<TreeNodeViewModel>(
            group.Select(mod => new ModEntryNodeViewModel((ILibraryMod)mod))
        );
        _children.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasChildren));
    }
}
