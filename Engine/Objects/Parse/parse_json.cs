using System.Text.Json;

namespace ImperiumCore.Classes.Prase;

public class parse_json : ImpParse
{
    protected override void Parse(string content)
    {
        using var doc = JsonDocument.Parse(content);
        Flatten(doc.RootElement, "", data);
    }

    private static void Flatten(JsonElement el, string prefix, Dictionary<string, object?> dict)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var key = prefix.Length > 0 ? $"{prefix}.{prop.Name}" : prop.Name;
                Flatten(prop.Value, key, dict);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            dict[prefix] = el.EnumerateArray().Select(GetValue).ToList();
        }
        else
        {
            dict[prefix] = GetValue(el);
        }
    }

    private static object? GetValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        _                    => el.ToString()
    };
}
