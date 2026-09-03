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
        SizeText = FormatSize(node.Data!.Size);
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1L << 10 => $"{bytes} B",
            < 1L << 20 => $"{bytes / (1.0 * (1 << 10)):0.#} KB",
            < 1L << 30 => $"{bytes / (1.0 * (1 << 20)):0.#} MB",
            _ => $"{bytes / (1.0 * (1 << 30)):0.#} GB",
        };
}
