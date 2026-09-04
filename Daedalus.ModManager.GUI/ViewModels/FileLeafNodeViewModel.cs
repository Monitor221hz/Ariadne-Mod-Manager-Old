using Daedalus.Contracts.Mods;
using Daedalus.VFS;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class FileLeafNodeViewModel : TreeNodeViewModel
{
    private readonly VirtualNode<ModFileEntry> _node;
    private string _sizeText;

    private string _displayName;
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    public override string SizeText
    {
        get => _sizeText;
        set => this.RaiseAndSetIfChanged(ref _sizeText, value);
    }
    public override long SizeBytes =>
        _node.Data!.Size + (HasChildren ? _node.Children.Sum(c => c.Data?.Size ?? 0) : 0);
    public ModEntryKind Kind => _node.Data!.Kind;

    public override bool RenameAllowed => false;
    public override IEnumerable<TreeNodeViewModel> Children =>
        _node.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _node.Children.Count > 0;

    public FileLeafNodeViewModel(VirtualNode<ModFileEntry> node)
    {
        _node = node;
        _sizeText = DiskSize.Format(node.Data!.Size);
        _displayName = node.Data!.Name;
    }
}
