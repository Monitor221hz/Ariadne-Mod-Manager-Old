using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class ModListEntry(ILibraryMod mod, bool active) : IModListEntry
{
    public ILibraryMod Mod { get; } = mod;
    public bool Active { get; set; } = active;
}
