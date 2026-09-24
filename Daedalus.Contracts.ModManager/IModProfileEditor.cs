namespace Daedalus.Contracts.ModManager;

public interface IModProfileEditor
{
    Task RemoveModAsync(IModProfile profile, ILibraryMod mod);
    Task DissolveGroupAsync(IModProfile profile, IModGroup group);
}
