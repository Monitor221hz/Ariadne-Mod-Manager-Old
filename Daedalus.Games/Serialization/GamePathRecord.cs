namespace Daedalus.Games.Serialization;

public sealed record class GamePathRecord(
    string Key,
    string DirectoryPath,
    List<string> Patterns,
    string? BasedOn = null
);
