namespace Daedalus.Contracts.Games;

public interface IPlatformConfiguration
{
    PlatformType Platform { get; }
    IReadOnlyList<IExecutable> Executables { get; }
    int DefaultIndex { get; }
}
