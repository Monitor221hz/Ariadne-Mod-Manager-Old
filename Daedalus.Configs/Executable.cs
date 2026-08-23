using Daedalus.Contracts.Configs;

namespace Daedalus.Configs;

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
