using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class FileLeafNodeViewModel : TreeNodeViewModel
{
    private readonly VirtualNode<ModFileEntry> _node;

    public override string DisplayName => _node.Data!.Name;
    public override string SizeText { get; }
    public ModEntryKind Kind => _node.Data!.Kind;
    public override IEnumerable<TreeNodeViewModel> Children =>
        _node.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _node.Children.Count > 0;

    public FileLeafNodeViewModel(VirtualNode<ModFileEntry> node)
    {
        _node = node;
        SizeText = DiskSize.Format(node.Data!.Size);
    }
}
