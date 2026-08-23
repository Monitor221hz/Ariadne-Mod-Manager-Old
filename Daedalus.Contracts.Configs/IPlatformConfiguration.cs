namespace Daedalus.Contracts.Configs;

public interface IPlatformConfiguration
{
    PlatformType Platform { get; }
    IReadOnlyList<IExecutable> Executables { get; }
    int DefaultIndex { get; }
}
