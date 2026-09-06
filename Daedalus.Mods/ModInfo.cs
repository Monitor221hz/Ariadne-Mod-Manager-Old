using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public sealed class ModInfo : IModInfo
{
    public ulong ID { get; }
    public SourceType IDSource { get; }
    public string Version { get; }
    public List<string> Categories { get; }
    public string Target { get; set; }
    public uint Priority { get; set; }
    public bool Active { get; set; }

    public ModInfo(
        ulong id,
        SourceType idSource,
        string version,
        List<string> categories,
        string target,
        uint priority,
        bool active
    )
    {
        ID = id;
        IDSource = idSource;
        Version = version;
        Categories = categories;
        Target = target;
        Priority = priority;
        Active = active;
    }
}
