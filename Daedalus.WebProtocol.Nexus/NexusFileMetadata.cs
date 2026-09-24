namespace Daedalus.WebProtocol.Nexus;

public sealed record NexusFileMetadata(
    int FileId,
    string? Name,
    string? FileName,
    string? Version,
    long? SizeInBytes
);
