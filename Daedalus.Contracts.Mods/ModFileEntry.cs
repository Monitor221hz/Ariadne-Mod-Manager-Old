namespace Daedalus.Contracts.Mods;

public enum ModEntryKind
{
    File,
    Directory,
    Archive,
}

public sealed record class ModFileEntry(
    string Name,
    ModEntryKind Kind,
    string AbsolutePath,
    long Size,
    DateTimeOffset LastModifiedUtc
);
