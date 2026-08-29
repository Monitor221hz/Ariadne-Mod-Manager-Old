namespace Daedalus.Contracts.Mods;

public interface ILoadOrderInfo
{
    IModInfo Origin { get; }
    IReadOnlyList<FileInfo> Artifacts { get; }
    IReadOnlyList<ILoadOrderInfo> Dependencies { get; }
    bool Active { get; set; }
}
