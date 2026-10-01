namespace Ariadne.Contracts.ModManager;

public interface IModListEntry
{
    ILibraryMod Mod { get; }
    bool Active { get; set; }
}
