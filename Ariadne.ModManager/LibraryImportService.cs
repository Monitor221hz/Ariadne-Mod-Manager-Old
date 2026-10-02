using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class LibraryImportService(
    IModManagerPaths paths,
    ILibraryModSerializer modSerializer
) : ILibraryImportService
{
    public IReadOnlyList<ILibraryMod> ScanLibrary()
    {
        var modsFolder = paths.ModsFolder;
        modsFolder.Refresh();
        if (!modsFolder.Exists)
        {
            return [];
        }
        var mods = new List<ILibraryMod>();
        foreach (var folder in modsFolder.EnumerateDirectories())
        {
            try
            {
                mods.Add(modSerializer.Load(folder));
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.Text.Json.JsonException
                ) { }
        }
        return mods;
    }

    public IReadOnlyList<ILibraryMod> FindMissingMods(IModProfile target, IModProfile source) =>
        source
            .ModList.Where(entry => !target.ModList.Contains(entry.Mod))
            .Select(entry => entry.Mod)
            .ToList();
}
