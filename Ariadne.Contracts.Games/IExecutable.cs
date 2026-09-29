namespace Ariadne.Contracts.Games;

public interface IExecutable
{
    FileInfo File { get; }
    IReadOnlyList<string> Arguments { get; }
}
