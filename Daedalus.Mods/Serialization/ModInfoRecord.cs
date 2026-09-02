using System.Text.Json.Serialization;
using Daedalus.Contracts.Mods;

namespace Daedalus.Mods.Serialization;

public sealed record class ModInfoRecord(
    ulong ID,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SourceType IDSource,
    string Version,
    List<string> Categories,
    string Target,
    uint Priority
);
