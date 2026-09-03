using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public sealed class ModInfo : IModInfo
{
    public ulong ID { get; }
    public string Name { get; }
    public SourceType IDSource { get; }
    public string Version { get; }
    public List<string> Categories { get; }
    public string Target { get; set; }
    public uint Priority { get; set; }

    public ModInfo(
        ulong id,
        string name,
        SourceType idSource,
        string version,
        List<string> categories,
        string target,
        uint priority
    )
    {
        ID = id;
        Name = name;
        IDSource = idSource;
        Version = version;
        Categories = categories;
        Target = target;
        Priority = priority;
    }
}
