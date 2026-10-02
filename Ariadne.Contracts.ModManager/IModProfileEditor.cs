namespace Ariadne.Contracts.ModManager;

public interface IModProfileEditor
{
    Task RemoveModAsync(IModProfile profile, ILibraryMod mod);
    Task DissolveGroupAsync(IModProfile profile, IModGroup group);

    bool TryAddMod(IModProfile profile, ILibraryMod mod);

    IModGroup CreateGroup(IModProfile profile);
}
