namespace Daedalus.Contracts.ModManager;

public interface IModInfo
{
    ulong ID { get; }
    SourceType IDSource { get; }
    string Version { get; }
    List<string> Categories { get; }
    string Target { get; set; }
    uint Priority { get; set; }
    bool Active { get; set; }
}
