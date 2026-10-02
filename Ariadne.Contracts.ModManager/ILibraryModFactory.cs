using System.Diagnostics.CodeAnalysis;

namespace Ariadne.Contracts.ModManager;

public interface ILibraryModFactory
{
    ILibraryMod Create(string name, IModInfo info);

    ILibraryMod Create(IModInfo info);

    ILibraryMod Open(DirectoryInfo folder);

    bool TryCreate(string name, IModInfo info, [NotNullWhen(true)] out ILibraryMod? mod);
}
