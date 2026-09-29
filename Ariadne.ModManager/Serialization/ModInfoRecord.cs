using System.Text.Json.Serialization;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager.Serialization;

public sealed record class ModInfoRecord(
    ulong ID,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SourceType IDSource,
    string Version,
    List<string> Categories,
    string Target,
    uint Priority,
    bool IsEnabled
);
