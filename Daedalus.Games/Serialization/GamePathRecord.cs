namespace Daedalus.Games.Serialization;

public sealed record class GamePathRecord(
    string Key,
    string DirectoryPath,
    List<string> Aliases,
    List<string> Patterns,
    string? BasedOn = null
);
