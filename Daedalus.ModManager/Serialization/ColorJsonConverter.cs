using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.ModManager.Serialization;

public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var text = reader.GetString();
        if (
            text is { Length: 9 }
            && text[0] == '#'
            && int.TryParse(
                text.AsSpan(1),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int argb
            )
        )
        {
            return Color.FromArgb(argb);
        }
        throw new JsonException($"Invalid color \"{text}\". Expected \"#AARRGGBB\".");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
        writer.WriteStringValue($"#{value.ToArgb():X8}");
}
