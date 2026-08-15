using System.Collections;
using System.Globalization;
using System.Text;

namespace ImperiumEngine.Files;

public class TomlTable
{
    public readonly Dictionary<string, object> values = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, TomlTable> tables = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, List<TomlTable>> arrays = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        values[key] = value;
    }

    public TomlTable EnsureTable(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return this;
        }
        if (tables.TryGetValue(name, out TomlTable existing))
        {
            return existing;
        }
        TomlTable t = new();
        tables[name] = t;
        return t;
    }

    public TomlTable AddArrayTable(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new TomlTable();
        }
        if (!arrays.TryGetValue(name, out List<TomlTable> list))
        {
            list = new List<TomlTable>();
            arrays[name] = list;
        }
        TomlTable t = new();
        list.Add(t);
        return t;
    }

    public TomlTable GetTable(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        tables.TryGetValue(name, out TomlTable t);
        return t;
    }

    public List<TomlTable> GetArray(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new List<TomlTable>();
        }
        if (arrays.TryGetValue(name, out List<TomlTable> list))
        {
            return list;
        }
        return new List<TomlTable>();
    }

    public string GetString(string key, string fallback = "")
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        if (v is string s)
        {
            return s;
        }
        return Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        try
        {
            return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public float GetFloat(string key, float fallback = 0)
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        try
        {
            return Convert.ToSingle(v, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public bool GetBool(string key, bool fallback = false)
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        if (v is bool b)
        {
            return b;
        }
        string s = Convert.ToString(v, CultureInfo.InvariantCulture);
        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return fallback;
    }

    public float[] GetFloats(string key, float[] fallback = null)
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        if (v is float[] fa)
        {
            return fa;
        }
        if (v is IList list)
        {
            float[] arr = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    arr[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
                }
                catch
                {
                    arr[i] = 0;
                }
            }
            return arr;
        }
        return fallback;
    }

    public string[] GetStrings(string key, string[] fallback = null)
    {
        if (!values.TryGetValue(key, out object v) || v == null)
        {
            return fallback;
        }
        if (v is string[] sa)
        {
            return sa;
        }
        if (v is IList list)
        {
            string[] arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                arr[i] = list[i] == null ? "" : Convert.ToString(list[i], CultureInfo.InvariantCulture) ?? "";
            }
            return arr;
        }
        return fallback;
    }
}

public class TomlDoc
{
    public TomlTable root = new();

    public static TomlDoc Parse(string text)
    {
        TomlDoc doc = new();
        if (string.IsNullOrEmpty(text))
        {
            return doc;
        }

        TomlTable current = doc.root;
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = StripComment(lines[i]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("[[") && line.EndsWith("]]"))
            {
                string name = line.Substring(2, line.Length - 4).Trim();
                current = doc.root.AddArrayTable(name);
                continue;
            }
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string name = line.Substring(1, line.Length - 2).Trim();
                current = doc.root.EnsureTable(name);
                continue;
            }

            int eq = IndexOfUnquoted(line, '=');
            if (eq < 0)
            {
                continue;
            }
            string key = line.Substring(0, eq).Trim();
            string raw = line.Substring(eq + 1).Trim();
            object val = ParseValue(raw);
            if (val != null)
            {
                current.Set(key, val);
            }
        }
        return doc;
    }

    public string Write()
    {
        StringBuilder sb = new();
        WriteTable(sb, root, null);
        return sb.ToString();
    }

    static void WriteTable(StringBuilder sb, TomlTable table, string header)
    {
        if (!string.IsNullOrEmpty(header))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }
            sb.Append('[').Append(header).AppendLine("]");
        }

        foreach (KeyValuePair<string, object> kv in table.values)
        {
            sb.Append(kv.Key).Append(" = ").AppendLine(FormatValue(kv.Value));
        }

        foreach (KeyValuePair<string, TomlTable> kv in table.tables)
        {
            WriteTable(sb, kv.Value, kv.Key);
        }

        foreach (KeyValuePair<string, List<TomlTable>> kv in table.arrays)
        {
            for (int i = 0; i < kv.Value.Count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append("[[").Append(kv.Key).AppendLine("]]");
                foreach (KeyValuePair<string, object> field in kv.Value[i].values)
                {
                    sb.Append(field.Key).Append(" = ").AppendLine(FormatValue(field.Value));
                }
            }
        }
    }

    static string FormatValue(object value)
    {
        if (value == null)
        {
            return "\"\"";
        }
        if (value is bool b)
        {
            return b ? "true" : "false";
        }
        if (value is string s)
        {
            return "\"" + Escape(s) + "\"";
        }
        if (value is float f)
        {
            return f.ToString("G9", CultureInfo.InvariantCulture);
        }
        if (value is double d)
        {
            return d.ToString("G17", CultureInfo.InvariantCulture);
        }
        if (value is int or long or short or byte)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        if (value is float[] fa)
        {
            return FormatList(fa);
        }
        if (value is string[] sa)
        {
            return FormatList(sa);
        }
        if (value is IList list)
        {
            return FormatList(list);
        }
        return "\"" + Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "") + "\"";
    }

    static string FormatList(IList list)
    {
        StringBuilder sb = new();
        sb.Append('[');
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append(FormatValue(list[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    static string StripComment(string line)
    {
        int cut = IndexOfUnquoted(line, '#');
        if (cut < 0)
        {
            return line;
        }
        return line.Substring(0, cut);
    }

    static int IndexOfUnquoted(string s, char ch)
    {
        bool quote = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\'))
            {
                quote = !quote;
                continue;
            }
            if (!quote && c == ch)
            {
                return i;
            }
        }
        return -1;
    }

    static object ParseValue(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }
        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
        {
            return Unescape(raw.Substring(1, raw.Length - 2));
        }
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (raw[0] == '[' && raw[raw.Length - 1] == ']')
        {
            return ParseArray(raw.Substring(1, raw.Length - 2));
        }
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long li))
        {
            if (li >= int.MinValue && li <= int.MaxValue)
            {
                return (int)li;
            }
            return li;
        }
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float fl))
        {
            return fl;
        }
        return raw;
    }

    static object ParseArray(string inner)
    {
        List<object> items = new();
        int start = 0;
        bool quote = false;
        for (int i = 0; i <= inner.Length; i++)
        {
            char c = i < inner.Length ? inner[i] : ',';
            if (c == '"' && (i == 0 || inner[i - 1] != '\\'))
            {
                quote = !quote;
            }
            if (quote || c != ',')
            {
                continue;
            }
            string piece = inner.Substring(start, i - start).Trim();
            start = i + 1;
            if (piece.Length == 0)
            {
                continue;
            }
            items.Add(ParseValue(piece));
        }

        bool all_num = items.Count > 0;
        bool all_str = items.Count > 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not (int or long or float or double))
            {
                all_num = false;
            }
            if (items[i] is not string)
            {
                all_str = false;
            }
        }
        if (all_num)
        {
            float[] arr = new float[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                arr[i] = Convert.ToSingle(items[i], CultureInfo.InvariantCulture);
            }
            return arr;
        }
        if (all_str)
        {
            string[] arr = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                arr[i] = (string)items[i];
            }
            return arr;
        }
        return items;
    }

    static string Unescape(string s)
    {
        return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}

public class File_TOML : ImpFile
{
    public TomlDoc doc = new();

    public bool Parse()
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
        {
            return false;
        }
        try
        {
            doc = TomlDoc.Parse(File.ReadAllText(filepath));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool WriteDoc()
    {
        if (string.IsNullOrEmpty(filepath) || doc == null)
        {
            return false;
        }
        try
        {
            string dir = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filepath, doc.Write());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override bool File_Read(object target)
    {
        return Parse();
    }

    public override bool File_Write(object target)
    {
        return WriteDoc();
    }
}
