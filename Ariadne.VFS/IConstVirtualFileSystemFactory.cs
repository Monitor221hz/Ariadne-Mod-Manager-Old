namespace Ariadne.VFS;

public class ConstVirtualFileSystemFactory<T>(
    Func<T> producer,
    VirtualFileSystemCapabilities capabilities = VirtualFileSystemCapabilities.None
) : IVirtualFileSystemFactory
    where T : class, IVirtualFileSystem
{
    private readonly Func<T> _producer = producer;

    public VirtualFileSystemCapabilities Capabilities { get; } = capabilities;

    public IVirtualFileSystem Create() => _producer();
}
