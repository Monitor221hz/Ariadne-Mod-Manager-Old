namespace Daedalus.Contracts.Mods;

public interface ILoadOrderInfo
{
    IModInfo Origin { get; }
    List<FileInfo> Artifacts { get; }
    List<ILoadOrderInfo> Dependencies { get; }
    bool Active { get; set; }
}
