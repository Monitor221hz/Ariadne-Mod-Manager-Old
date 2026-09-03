using System.Collections.Generic;
using System.Linq;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.ModManager.GUI.ViewModels;

internal static class ContentNodeViewModel
{
    public static TreeNodeViewModel Wrap(VirtualNode<ModFileEntry> node) =>
        node.IsDirectory ? new DirectoryNodeViewModel(node) : new FileLeafNodeViewModel(node);
}
