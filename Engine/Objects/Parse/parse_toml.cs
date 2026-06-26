using Tomlyn;
using Tomlyn.Model;

namespace ImperiumCore.Classes.Prase;

public class parse_toml : ImpParse
{
    // internal: lets ImpSave wrap a raw sub-table without going through ParseString
    internal parse_toml(TomlTable table) => Load(table);
    public   parse_toml() { }

    protected override void Parse(string content) => Load(Toml.ToModel(content));

    private void Load(TomlTable table)
    {
        foreach (var (key, value) in table)
            data[key] = value;
    }

    public override ImpParse? GetTable(string field)
    {
        if (data.TryGetValue(field, out var val) && val is TomlTable sub)
            return new parse_toml(sub);
        return null;
    }

    public override IEnumerable<string> Keys => data.Keys;
}
