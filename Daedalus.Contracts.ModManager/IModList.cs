namespace Daedalus.Contracts.ModManager;

public interface IModList : IList<ILibraryMod>
{
    IList<ILibraryMod> LooseMods { get; }
    IList<IModGroup> ModGroups { get; }
}
