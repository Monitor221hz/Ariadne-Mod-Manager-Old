using Daedalus.Contracts.Games;

namespace Daedalus.Games;

public class Executable : IExecutable
{
    public FileInfo File { get; }
    public IReadOnlyList<string> Arguments { get; }

    public Executable(FileInfo file, IReadOnlyList<string> arguments)
    {
        File = file;
        Arguments = arguments;
    }
}
