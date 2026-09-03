using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DirectoryNodeViewModel : TreeNodeViewModel
{
    private readonly VirtualNode<ModFileEntry> _node;

    public override string DisplayName => _node.Name;
    public override IEnumerable<TreeNodeViewModel> Children =>
        _node.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _node.Children.Count > 0;

    public DirectoryNodeViewModel(VirtualNode<ModFileEntry> node)
    {
        _node = node;
    }
}
