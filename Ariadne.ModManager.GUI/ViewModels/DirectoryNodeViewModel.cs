using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using ByteSizeLib;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class DirectoryNodeViewModel : ContentNodeViewModel
{
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

    public DirectoryNodeViewModel(VirtualNode<ModFileEntry> node, ModEntryNodeViewModel owner)
        : base(node, owner)
    {
        _sizeText = ByteSize.FromBytes(SizeBytes).ToString();
        _displayName = node.Name;
    }
}
