using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class ModInfo : IModInfo
{
    public ModID ID { get; }
    public string Version { get; }
    public List<string> Categories { get; }
    public string Target { get; set; }
    public uint Priority { get; set; }
    public bool Active { get; set; }

    public ModInfo(
        ModID id,
        string version,
        List<string> categories,
        string target,
        uint priority,
        bool active
    )
    {
        ID = id;
        Version = version;
        Categories = categories;
        Target = target;
        Priority = priority;
        Active = active;
    }

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
        ID = new ModID(id, idSource);
        Version = version;
        Categories = categories;
        Target = target;
        Priority = priority;
        Active = active;
    }
}
