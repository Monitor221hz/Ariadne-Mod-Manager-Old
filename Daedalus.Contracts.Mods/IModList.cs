using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.Mods;

public interface IModList : IList<ILibraryMod>
{
    IList<ILibraryMod> LooseMods { get; }
    IList<IModGroup> ModGroups { get; }
}
