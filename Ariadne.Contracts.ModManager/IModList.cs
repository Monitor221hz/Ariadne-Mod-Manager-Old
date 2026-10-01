namespace Ariadne.Contracts.ModManager;

public interface IModList : IList<IModListEntry>
{
    IList<IModListEntry> LooseMods { get; }
    IList<IModGroup> ModGroups { get; }

    bool Contains(ILibraryMod mod);
}
