using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using ByteSizeLib;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class ArchiveLeafNodeViewModel : ContentNodeViewModel
{
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
    }
    public ModEntryKind Kind => Node.Data!.Kind;
    public string AbsolutePath => Node.Data!.AbsolutePath;

    public ArchiveLeafNodeViewModel(VirtualNode<ModFileEntry> node, ContentHostNodeViewModel owner)
        : base(node, owner)
    {
        _sizeText = ByteSize.FromBytes(node.Data!.Size).ToString();
        _displayName = node.Data!.Name;
    }
}
