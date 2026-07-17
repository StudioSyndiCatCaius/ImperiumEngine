using System.Text;

namespace ImperiumCore.Classes.Prase;

public class parse_csv : ImpParse
{
    public List<Dictionary<string, string>> Rows { get; } = new();

    protected override void Parse(string content)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        var headers = SplitLine(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            var values = SplitLine(lines[i]);
            var row = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length && j < values.Length; j++)
                row[headers[j]] = values[j];
            Rows.Add(row);
            if (i == 1)
                foreach (var (k, v) in row) data[k] = v;
        }
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        foreach (char c in line)
        {
            if (c == '"') { inQuote = !inQuote; continue; }
            if (c == ',' && !inQuote) { fields.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(c);
        }
        fields.Add(sb.ToString().Trim());
        return fields.ToArray();
    }
}
