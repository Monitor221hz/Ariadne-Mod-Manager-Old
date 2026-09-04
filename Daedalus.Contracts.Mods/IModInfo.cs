namespace Daedalus.Contracts.Mods;

public interface IModInfo
{
    ulong ID { get; }
    SourceType IDSource { get; }
    string Version { get; }
    List<string> Categories { get; }
    string Target { get; set; }
    uint Priority { get; set; }
}
