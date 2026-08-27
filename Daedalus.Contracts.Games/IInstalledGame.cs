namespace Daedalus.Contracts.Games;

public interface IInstalledGame
{
    DirectoryInfo InstallPath { get; }
    ISupportedGame Configuration { get; }
    public string LookupAbsolutePath(IGamePath path);
    public string UpdateAbsolutePath(IGamePath path);
}
