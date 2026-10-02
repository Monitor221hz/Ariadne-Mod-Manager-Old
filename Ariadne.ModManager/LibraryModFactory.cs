using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class InstancedModFactory(
    IReadOnlyList<IArchiveReader> archiveReaders,
    IModManagerPaths paths
) : ILibraryModFactory
{
    private readonly IReadOnlyList<IArchiveReader> _archiveReaders = archiveReaders;
    private readonly IModManagerPaths _paths = paths;

    private static DirectoryInfo UniqueModFolder(DirectoryInfo modsFolder)
    {
        var candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, "New Mod"));
        for (var i = 2; candidate.Exists; i++)
        {
            candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, $"New Mod {i}"));
        }
        return candidate;
    }

    public ILibraryMod Create(string name, IModInfo info)
    {
        var directory = new DirectoryInfo(Path.Join(_paths.ModsFolder.FullName, name));
        ILibraryMod mod = new LibraryMod(info, directory, _archiveReaders);
        mod.TryCreateDirectory();
        return mod;
    }

    public ILibraryMod Create(IModInfo info)
    {
        var directory = UniqueModFolder(_paths.ModsFolder);
        directory.Create();
        return new LibraryMod(info, directory, _archiveReaders);
    }

    public ILibraryMod Open(DirectoryInfo folder)
    {
        return new LibraryMod(
            new ModInfo(0, SourceType.Local, "", [], ""),
            folder,
            _archiveReaders
        );
    }

    public bool TryCreate(string name, IModInfo info, [NotNullWhen(true)] out ILibraryMod? mod)
    {
        var directory = new DirectoryInfo(Path.Join(_paths.ModsFolder.FullName, name));
        mod = new LibraryMod(info, directory, _archiveReaders);
        if (!mod.TryCreateDirectory())
        {
            mod = null;
            return false;
        }
        return true;
    }
}
