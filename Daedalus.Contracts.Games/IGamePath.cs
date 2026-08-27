namespace Daedalus.Contracts.Games;

public interface IGamePath
{
    string Key { get; }
    string? BasedOn { get; }
    string DirectoryPath { get; set; }
    IReadOnlyList<string> Patterns { get; }

    string GetAbsolutePath(IInstalledGame game);
}
