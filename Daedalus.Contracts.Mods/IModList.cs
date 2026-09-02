using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.Mods;

public interface IModList : IList<IModInfo>
{
    IList<IModInfo> LooseMods { get; }
    IList<IModGroup> ModGroups { get; }
}
