using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace ImperiumEngine.Main;

// Reads/writes Color as a hex string so theme files stay hand-editable.
// Accepts "#RGB", "#RRGGBB", "#RRGGBBAA" (with or without the #), and also the plain
// { "R":30, "G":30, "B":34, "A":255 } object form.
public class JsonColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return Color_FromHex(reader.GetString() ?? "");

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            byte r = 0, g = 0, b = 0, a = 255;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                string key = reader.GetString() ?? "";
                reader.Read();
                byte v = (byte)Math.Clamp(reader.GetInt32(), 0, 255);

                switch (key.ToUpperInvariant())
                {
                    case "R": r = v; break;
                    case "G": g = v; break;
                    case "B": b = v; break;
                    case "A": a = v; break;
                }
            }

            return new Color(r, g, b, a);
        }

        return Color.Blank;
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.A == 255
            ? $"#{value.R:X2}{value.G:X2}{value.B:X2}"
            : $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}");
    }

    public static Color Color_FromHex(string hex)
    {
        hex = hex.Trim().TrimStart('#');

        if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}")); //#abc -> #aabbcc
        if (hex.Length == 6) hex += "FF";
        if (hex.Length != 8) return Color.Blank;

        try
        {
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                Convert.ToByte(hex.Substring(6, 2), 16));
        }
        catch
        {
            return Color.Blank;
        }
    }
}
