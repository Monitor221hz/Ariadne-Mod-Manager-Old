using Ariadne.Contracts.ModManager;
using Ariadne.VFS;

namespace Ariadne.ModManager.GUI.ViewModels;

public abstract class ContentNodeViewModel : TreeNodeViewModel
{
    protected ContentNodeViewModel(VirtualNode<ModFileEntry> node, ModEntryNodeViewModel owner)
    {
        Node = node;
        Owner = owner;
    }

    public VirtualNode<ModFileEntry> Node { get; }
    public ModEntryNodeViewModel Owner { get; }

    public bool IsDiskBacked
    {
        get
        {
            for (var current = Node.Parent; current is not null; current = current.Parent)
            {
                if (!current.IsDirectory)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public string DiskPath
    {
        get
        {
            var parts = new Stack<string>();
            for (var current = Node; current.Parent is not null; current = current.Parent)
            {
                parts.Push(current.Name);
            }
            return Path.Join([Owner.Model.Directory.FullName, .. parts]);
        }
    }

    public override long SizeBytes => Node.SelfAndDescendants().Sum(n => n.Data?.Size ?? 0);

    public override IEnumerable<TreeNodeViewModel> Children =>
        Node.Children.Select(child => Wrap(child, Owner));

    public override bool HasChildren => Node.Children.Count > 0;

    public override bool RenameAllowed => false;

    public static TreeNodeViewModel Wrap(
        VirtualNode<ModFileEntry> node,
        ModEntryNodeViewModel owner
    ) =>
        node.IsDirectory ? new DirectoryNodeViewModel(node, owner)
        : node.Data!.Kind == ModEntryKind.Archive ? new ArchiveLeafNodeViewModel(node, owner)
        : new FileLeafNodeViewModel(node, owner);
}
