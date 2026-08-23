namespace Daedalus.Mods;

public class ModInfo
{
    public ulong ID { get; }
    public string Name { get; }
    public SourceType IDSource { get; }
    public string Version { get; }
    public List<string> Categories { get; }

    public ModInfo(
        ulong id,
        string name,
        SourceType idSource,
        string version,
        List<string> categories
    )
    {
        ID = id;
        Name = name;
        IDSource = idSource;
        Version = version;
        Categories = categories;
    }
}
