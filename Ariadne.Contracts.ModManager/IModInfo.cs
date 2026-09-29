namespace Ariadne.Contracts.ModManager;

public sealed record ModID(ulong Value, SourceType Source);

public interface IModInfo
{
    ModID ID { get; }
    string Version { get; }
    List<string> Categories { get; }
    string Target { get; set; }
    uint Priority { get; set; }
    bool Active { get; set; }
}
