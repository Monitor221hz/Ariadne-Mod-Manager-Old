namespace Daedalus.Contracts.Configs;

public interface IExecutable
{
    FileInfo File { get; }
    IReadOnlyList<string> Arguments { get; }
}
