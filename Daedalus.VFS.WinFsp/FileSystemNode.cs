namespace Daedalus.VFS.WinFsp;

public class FileSystemNode
{
    public VirtualNode<BackedEntry> Node { get; set; }

    public uint OpenCount { get; private set; }

    public bool PendingDelete { get; set; }

    public FileSystemNode(VirtualNode<BackedEntry> node)
    {
        Node = node;
        OpenCount = 0;
        PendingDelete = false;
    }

    public uint AddOpen() => ++OpenCount;

    public uint RemoveOpen() => OpenCount == 0 ? 0 : --OpenCount;
}
