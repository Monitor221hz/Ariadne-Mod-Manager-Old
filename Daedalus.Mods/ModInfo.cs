using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public abstract class LoadOrderInfo : ILoadOrderInfo
{
    public IModInfo Origin { get; }
    public List<FileInfo> Artifacts { get; }
    public List<ILoadOrderInfo> Dependencies { get; }
    public bool Active { get; set; }
}

public class ModInfo : IModInfo
{
    public ulong ID { get; }
    public string Name { get; }
    public DirectoryInfo Directory { get; }
    public SourceType IDSource { get; }
    public string Version { get; }
    public List<string> Categories { get; }
    public string Target { get; set; }
    public uint Priority { get; set; } = 0;

    public ModInfo(
        ulong id,
        string name,
        DirectoryInfo directory,
        SourceType idSource,
        string version,
        List<string> categories,
        string target
    )
    {
        ID = id;
        Name = name;
        Directory = directory;
        IDSource = idSource;
        Version = version;
        Categories = categories;
        Target = target;
    }
}
