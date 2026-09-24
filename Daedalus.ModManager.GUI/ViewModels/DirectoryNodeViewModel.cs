using ByteSizeLib;
using Daedalus.Contracts.ModManager;
using Daedalus.VFS;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DirectoryNodeViewModel : TreeNodeViewModel
{
    private readonly VirtualNode<ModFileEntry> _node;
    private string _displayName;
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    private string _sizeText;
    public override string SizeText
    {
        get => _sizeText;
    }
    public override bool RenameAllowed => false;

    public override long SizeBytes => _node.SelfAndDescendants().Sum(n => n.Data?.Size ?? 0);

    public override IEnumerable<TreeNodeViewModel> Children =>
        _node.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _node.Children.Count > 0;

    public DirectoryNodeViewModel(VirtualNode<ModFileEntry> node)
    {
        _node = node;
        _sizeText = ByteSize.FromBytes(SizeBytes).ToString();
        _displayName = node.Name;
    }
}
