using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Daedalus.Contracts.Mods;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class GroupHeaderNodeViewModel : TreeNodeViewModel
{
    private readonly List<TreeNodeViewModel> _children;

    public override string DisplayName { get; }
    public IBrush SeparatorBrush { get; }
    public override IEnumerable<TreeNodeViewModel> Children => _children;
    public override bool HasChildren => true;

    public GroupHeaderNodeViewModel(IModGroup group)
    {
        DisplayName = group.Name;
        var color = group.HeaderColor;
        SeparatorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        _children = group.Select(mod => (TreeNodeViewModel)new ModEntryNodeViewModel(mod)).ToList();
    }
}
