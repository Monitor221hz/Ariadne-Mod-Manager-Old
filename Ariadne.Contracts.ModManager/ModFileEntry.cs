namespace Ariadne.Contracts.ModManager;

public enum ModEntryKind
{
    File,
    Directory,
    Archive,
}

public sealed record class ModFileEntry(
    string Name,
    ModEntryKind Kind,
    IModInfo Origin,
    string AbsolutePath,
    long Size,
    DateTimeOffset LastModifiedUtc
);
