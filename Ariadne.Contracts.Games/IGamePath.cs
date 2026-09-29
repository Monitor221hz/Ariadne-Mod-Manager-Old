namespace Ariadne.Contracts.Games;

public interface IGamePath : IEqualityComparer<IGamePath>, IEquatable<IGamePath>
{
    string Key { get; }
    string? BasedOn { get; }
    string DirectoryPath { get; }
    IReadOnlyCollection<string> Aliases { get; }
    IReadOnlyList<string> Patterns { get; }
    string GetAbsolutePath(IInstalledGame game);
}
