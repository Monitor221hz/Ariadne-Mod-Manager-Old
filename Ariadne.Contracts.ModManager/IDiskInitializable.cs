namespace Ariadne.Contracts.ModManager;

public interface IDiskInitializable
{
    void InitializeDisk();

    public async Task InitializeDiskAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => InitializeDisk(), cancellationToken);
    }
}
