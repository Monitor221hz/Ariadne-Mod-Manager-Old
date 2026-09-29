namespace Ariadne.VFS;

public class ConstVirtualFileSystemFactory<T>(Func<T> producer) : IVirtualFileSystemFactory
    where T : class, IVirtualFileSystem
{
    private readonly Func<T> _producer = producer;

    public IVirtualFileSystem Create() => _producer();
}
