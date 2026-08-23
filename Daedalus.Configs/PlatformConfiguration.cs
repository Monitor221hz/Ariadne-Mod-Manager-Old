using Daedalus.Contracts.Configs;

namespace Daedalus.Configs;

public class PlatformConfiguration : IPlatformConfiguration
{
    public PlatformType Platform { get; }
    public IReadOnlyList<IExecutable> Executables { get; }
    public int DefaultIndex { get; }

    public PlatformConfiguration(
        PlatformType platform,
        IReadOnlyList<IExecutable> executables,
        int defaultIndex
    )
    {
        Platform = platform;
        Executables = executables;
        DefaultIndex = defaultIndex;
    }
}
