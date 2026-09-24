using System.Text.Json.Serialization;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager.Serialization;

public sealed record class ModInfoRecord(
    ulong ID,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SourceType IDSource,
    string Version,
    List<string> Categories,
    string Target,
    uint Priority,
    bool IsEnabled
);
