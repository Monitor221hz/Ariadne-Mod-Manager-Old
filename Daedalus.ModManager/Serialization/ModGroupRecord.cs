using System.Drawing;
using System.Text.Json.Serialization;

namespace Daedalus.ModManager.Serialization;

public sealed record class ModGroupRecord(
    string Name,
    [property: JsonConverter(typeof(ColorJsonConverter))] Color HeaderColor,
    List<string> Mods
);
