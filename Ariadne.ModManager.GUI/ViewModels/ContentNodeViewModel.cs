using Ariadne.Contracts.ModManager;
using Ariadne.VFS;

namespace Ariadne.ModManager.GUI.ViewModels;

internal static class ContentNodeViewModel
{
    public static TreeNodeViewModel Wrap(VirtualNode<ModFileEntry> node) =>
        node.IsDirectory ? new DirectoryNodeViewModel(node)
        : node.Data!.Kind == ModEntryKind.Archive ? new ArchiveLeafNodeViewModel(node)
        : new FileLeafNodeViewModel(node);
}
