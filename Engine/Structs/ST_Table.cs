using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Engine.Core;
using Engine.Globals;
using Engine.Interfaces;
using Tomlyn;
using Tomlyn.Model;

namespace Engine.Structs;

public class TTable
{
    // ========================================================================================================
    // CLASS
    // ========================================================================================================
    public Dictionary<TLabel,object> data = new();

    public bool Has(TLabel key) { return data.ContainsKey(key); }

    // TOML has no distinct int/float literal syntax, so whole-number floats parse back as int.
    // Coerce between numeric leaf types instead of requiring an exact match.
    private static T Coerce<T>(object? value, T fallback)
    {
        if (value is T typed) return typed;
        if (value is bool or int or long or float or double)
        {
            try { return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }
        return fallback;
    }

    public T Get<T>(TLabel key, T fallback = default!)
    {
        string path = key.ToString();

        if (!path.Contains('.'))
        {
            if (!data.TryGetValue(key, out object? value))
                return fallback;

            return Coerce(value, fallback);
        }

        string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        TTable table = this;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            TLabel partKey = parts[i];

            if (!table.data.TryGetValue(partKey, out object? next) || next is not TTable nextTable)
                return fallback;

            table = nextTable;
        }

        TLabel finalKey = parts[^1];

        if (!table.data.TryGetValue(finalKey, out object? finalValue))
            return fallback;

        return Coerce(finalValue, fallback);
    }

    public void Set<T>(TLabel key, T value)
    {
        string path = key.ToString();

        if (!path.Contains('.'))
        {
            data[key] = value;
            return;
        }

        string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        TTable table = this;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            TLabel partKey = parts[i];

            if (!table.data.TryGetValue(partKey, out object? next) || next is not TTable nextTable)
            {
                nextTable = new TTable();
                table.data[partKey] = nextTable;
            }

            table = nextTable;
        }

        table.data[parts[^1]] = value;
    }
    public bool Remove(TLabel key) { return data.Remove(key); }
    public void Clear() { data.Clear(); }
    
    public string get_String(TLabel key, string fallback = "") { return Get<string>(key, fallback); }
    public int get_Int(TLabel key, int fallback = 0) { return Get<int>(key, fallback); }
    public float get_Float(TLabel key, float fallback = 0) { return Get<float>(key, fallback); }
    public bool get_Bool(TLabel key, bool fallback = false) { return Get<bool>(key, fallback); }
    public TTable get_Table(TLabel key) { return Get<TTable>(key); }
    public List<object> get_List(TLabel key)
    {
        object? value = Get<object>(key);
        if (value is List<object> list) return list;
        if (value is TTable table && table.data.Count == 0) return new();
        return new();
    }

    public static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is TTable ta && b is TTable tb)
        {
            if (ta.data.Count != tb.data.Count) return false;
            foreach (var pair in ta.data)
            {
                if (!tb.data.TryGetValue(pair.Key, out object? bv)) return false;
                if (!ValuesEqual(pair.Value, bv)) return false;
            }
            return true;
        }
        if (a is List<object> la && b is List<object> lb)
        {
            if (la.Count != lb.Count) return false;
            for (int i = 0; i < la.Count; i++)
                if (!ValuesEqual(la[i], lb[i])) return false;
            return true;
        }
        if (IsNum(a) && IsNum(b))
            return Convert.ToDouble(a, CultureInfo.InvariantCulture) == Convert.ToDouble(b, CultureInfo.InvariantCulture);
        if (a is TLabel al && b is string bs) return al.Value == bs;
        if (b is TLabel bl && a is string as_) return as_ == bl.Value;
        return a.Equals(b) || string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    static bool IsNum(object o) =>
        o is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    
    // ######################################################################################################
    // ######################################################################################################
    // STATIC
    // ######################################################################################################
    // ######################################################################################################
    
    // =========================================================================
    // JSON
    // =========================================================================

    public static TTable FromJSON(string json)
    {
        TTable table = new();

        object? Read(JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.Object => ReadTable(e),
                JsonValueKind.Array => ReadArray(e),
                JsonValueKind.String => e.GetString() ?? "",
                JsonValueKind.Number => e.TryGetInt32(out int i) ? i : e.GetSingle(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        TTable ReadTable(JsonElement e)
        {
            TTable t = new();

            foreach (JsonProperty prop in e.EnumerateObject())
            {
                object? value = Read(prop.Value);
                if (value != null)
                    t.data[prop.Name] = value;
            }

            return t;
        }

        List<object> ReadArray(JsonElement e)
        {
            List<object> list = new();
            foreach (JsonElement item in e.EnumerateArray())
            {
                object? value = Read(item);
                if (value != null)
                    list.Add(value);
            }
            return list;
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return table;

        return ReadTable(doc.RootElement);
    }

    public static string ToJSON(TTable table)
    {
        object Write(object value)
        {
            if (value is TTable t)
            {
                Dictionary<string, object> obj = new();

                foreach (var pair in t.data)
                    obj[pair.Key.ToString()] = Write(pair.Value);

                return obj;
            }

            if (value is List<object> list)
            {
                List<object> arr = new();
                foreach (object item in list)
                    arr.Add(Write(item));
                return arr;
            }

            return value;
        }

        return JsonSerializer.Serialize(Write(table), new JsonSerializerOptions { WriteIndented = true });
    }

    // =========================================================================
    // TOML
    // =========================================================================

    public static TTable FromTOML(string toml)
    {
        object Convert(object? value)
        {
            switch (value)
            {
                case TomlTable tt:
                {
                    TTable t = new();
                    foreach (var pair in tt)
                    {
                        if (pair.Value != null)
                            t.data[pair.Key] = Convert(pair.Value);
                    }
                    return t;
                }
                case TomlArray ta:
                {
                    List<object> list = new();
                    foreach (object? item in ta)
                    {
                        if (item != null)
                            list.Add(Convert(item));
                    }
                    return list;
                }
                case TomlTableArray tta:
                {
                    List<object> list = new();
                    foreach (TomlTable item in tta)
                        list.Add(Convert(item));
                    return list;
                }
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    return (int)l;
                case double d:
                    return (float)d;
                default:
                    return value ?? "";
            }
        }

        toml = toml.TrimStart('\uFEFF');
        TomlTable? model = TomlSerializer.Deserialize<TomlTable>(toml);
        if (model == null) return new();
        return Convert(model) as TTable ?? new();
    }

    public static string ToTOML(TTable table)
    {
        StringBuilder sb = new();

        string FormatScalar(object value)
        {
            return value switch
            {
                string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
                bool b => b ? "true" : "false",
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                _ => "\"" + value.ToString()?.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            };
        }

        void WriteValue(object value, int indent)
        {
            if (value is TTable t)
            {
                if (t.data.Count == 0) { sb.Append("{}"); return; }
                sb.AppendLine("{");
                string pad = new string(' ', (indent + 1) * 4);
                foreach (var pair in t.data)
                {
                    sb.Append(pad).Append(pair.Key).Append(" = ");
                    WriteValue(pair.Value, indent + 1);
                    sb.AppendLine(",");
                }
                sb.Append(new string(' ', indent * 4)).Append('}');
                return;
            }

            if (value is List<object> list)
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.AppendLine("[");
                string pad = new string(' ', (indent + 1) * 4);
                foreach (object item in list)
                {
                    sb.Append(pad);
                    WriteValue(item, indent + 1);
                    sb.AppendLine(",");
                }
                sb.Append(new string(' ', indent * 4)).Append(']');
                return;
            }

            sb.Append(FormatScalar(value));
        }

        foreach (var pair in table.data)
        {
            if (pair.Key.ToString() == "vars" && pair.Value is TTable)
                continue;

            sb.Append(pair.Key).Append(" = ");
            WriteValue(pair.Value, 0);
            sb.AppendLine();
        }

        if (table.data.TryGetValue("vars", out object? varsObj) && varsObj is TTable vars)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            sb.AppendLine("[vars]");
            foreach (var pair in vars.data)
            {
                sb.Append(pair.Key).Append(" = ");
                WriteValue(pair.Value, 0);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
    
    // =========================================================================
    // Object
    // =========================================================================

    // classes opt in per-member via [ImpVar]; structs (Vector3, TTransform3, etc.) auto-cascade
    // every public field as a subtable, since we don't own types like Vector3 to attribute them.
    private static IEnumerable<MemberInfo> Vars_Members(Type t)
    {
        if (t.IsValueType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                yield return f;
            yield break;
        }

        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (f.IsDefined(typeof(ImpVarAttribute), true))
                yield return f;

        foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.IsDefined(typeof(ImpVarAttribute), true))
                yield return p;
    }

    public static IEnumerable<MemberInfo> Config_Members(Type t, bool statics)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly
            | (statics ? BindingFlags.Static : BindingFlags.Instance);
        foreach (FieldInfo f in t.GetFields(flags))
            if (f.IsDefined(typeof(ImpVarAttribute), true) && f.IsDefined(typeof(ConfigAttribute), true))
                yield return f;
        foreach (PropertyInfo p in t.GetProperties(flags))
            if (p.CanRead && p.CanWrite
                && p.IsDefined(typeof(ImpVarAttribute), true)
                && p.IsDefined(typeof(ConfigAttribute), true))
                yield return p;
    }

    static object? MemberGet(MemberInfo member, object? host) =>
        member is FieldInfo f ? f.GetValue(host) : ((PropertyInfo)member).GetValue(host);

    static TTable Vars_Write(object o, HashSet<object> seen)
    {
        TTable vars = new();
        if (!o.GetType().IsValueType && !seen.Add(o))
            return vars;
        foreach (MemberInfo member in Vars_Members(o.GetType()))
        {
            object? val = MemberGet(member, o);
            if (val == null) continue;
            object? written = WriteVal(val, seen);
            if (written == null) continue;
            vars.Set(member.Name, written);
        }
        return vars;
    }

    static object? WriteVal(object val, HashSet<object> seen)
    {
        Type t = val.GetType();
        if (t.IsEnum) return val.ToString()!;
        if (val is bool or int or float or double or string) return val;
        if (val is TLabel lab) return lab.Value;
        if (val is I_Property ip && ip.Property_IsCustomParse()) return ip.Property_Write();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            List<object> arr = new();
            foreach (object? item in (IList)val)
            {
                if (item == null) continue;
                object? written = WriteVal(item, seen);
                if (written != null) arr.Add(written);
            }
            return arr;
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            TTable map = new();
            foreach (DictionaryEntry e in (IDictionary)val)
            {
                if (e.Key == null || e.Value == null) continue;
                object? written = WriteVal(e.Value, seen);
                if (written != null) map.Set(e.Key.ToString() ?? "", written);
            }
            return map;
        }
        if (!t.IsValueType && seen.Contains(val)) return null;
        return Vars_Write(val, seen);
    }

    public static TTable FromObject(object obj)
    {
        TTable table = new();
        table.Set("type", obj.GetType().Name);
        table.Set("vars", Vars_Write(obj, new HashSet<object>(ReferenceEqualityComparer.Instance)));
        return table;
    }

    public static TTable FromConfig(Type type, object? instance, bool statics)
    {
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        TTable vars = new();
        object? host = statics ? null : instance;
        foreach (MemberInfo member in Config_Members(type, statics))
        {
            object? val = MemberGet(member, host);
            if (val == null) continue;
            object? written = WriteVal(val, seen);
            if (written == null) continue;
            vars.Set(member.Name, written);
        }
        return vars;
    }

    public static void PopulateConfig(TTable vars, Type type, object? instance, bool statics)
    {
        if (vars == null || type == null) return;
        object? host = statics ? null : instance;
        foreach (MemberInfo member in Config_Members(type, statics))
        {
            if (!vars.Has(member.Name)) continue;
            try
            {
                object raw = vars.data[member.Name];
                Type member_type = member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;
                object? result = ReadVal(member_type, raw);
                if (result == null) continue;
                if (member is FieldInfo field) field.SetValue(host, result);
                else ((PropertyInfo)member).SetValue(host, result);
            }
            catch (Exception e)
            {
                GLog.Warning($"TTable: skipped config '{type.Name}.{member.Name}' ({e.Message})");
            }
        }
    }

    // populates [ImpVar]/struct members of an already-constructed object from a "vars" table.
    // exposed publicly (not just a ToObject-local closure) so I_Property types that need to
    // rebuild themselves in place (e.g. an inlined ImpAsset) can reuse it from Property_Read.
    public static void PopulateObject(TTable vars, object into)
    {
        if (vars == null || into == null) return;
        foreach (MemberInfo member in Vars_Members(into.GetType()))
        {
            if (!vars.Has(member.Name)) continue;

            try
            {
                object raw = vars.data[member.Name];
                Type member_type = member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;
                object? result = ReadVal(member_type, raw);
                if (result == null) continue;

                if (member is FieldInfo field) field.SetValue(into, result);
                else ((PropertyInfo)member).SetValue(into, result);
            }
            catch (Exception e)
            {
                GLog.Warning($"TTable: skipped '{member.Name}' ({e.Message})");
            }
        }
    }

    static object? ReadVal(Type member_type, object raw)
    {
        if (raw == null || member_type == null) return null;
        try
        {
            if (member_type.IsEnum)
            {
                if (raw is not string enum_str || !Enum.TryParse(member_type, enum_str, out object? ev))
                    return null;
                return ev;
            }
            if (member_type == typeof(bool) || member_type == typeof(int) || member_type == typeof(float) ||
                member_type == typeof(double) || member_type == typeof(string))
            {
                if (member_type.IsInstanceOfType(raw)) return raw;
                if (raw is not IConvertible) return null;
                return Convert.ChangeType(raw, member_type, CultureInfo.InvariantCulture);
            }
            if (member_type == typeof(TLabel))
                return TLabel.From(raw as string ?? raw.ToString());
            if (member_type.IsGenericType && member_type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elem = member_type.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(member_type)!;
                if (raw is List<object> arr)
                    foreach (object item in arr)
                    {
                        object? v = ReadVal(elem, item);
                        if (v != null) list.Add(v);
                    }
                return list;
            }
            if (member_type.IsGenericType && member_type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type kt = member_type.GetGenericArguments()[0];
                Type vt = member_type.GetGenericArguments()[1];
                IDictionary dict = (IDictionary)Activator.CreateInstance(member_type)!;
                if (raw is TTable map)
                {
                    foreach (var pair in map.data)
                    {
                        object? k = ReadVal(kt, pair.Key.Value);
                        object? v = ReadVal(vt, pair.Value);
                        if (k != null && v != null) dict[k] = v;
                    }
                }
                return dict;
            }
            if (typeof(ImpAsset).IsAssignableFrom(member_type))
            {
                if (raw is TTable inline)
                {
                    if (inline.data.Count == 0) return null;
                    string type_name = inline.get_String("type");
                    Type inst_type = member_type;
                    if (!string.IsNullOrEmpty(type_name))
                        inst_type = TClass<object>.Resolve(type_name) ?? member_type;
                    ImpAsset inst = Activator.CreateInstance(inst_type) as ImpAsset
                        ?? Activator.CreateInstance(member_type) as ImpAsset;
                    if (inst == null) return null;
                    inst.Property_Read(inline);
                    return inst;
                }
                string reference = raw as string ?? "";
                int colon = reference.IndexOf(':');
                string path = colon >= 0 ? reference[(colon + 1)..] : reference;
                return GAsset.Asset_Load(path, member_type);
            }
            object? created = Activator.CreateInstance(member_type);
            if (created == null) return null;
            if (created is I_Property ip && ip.Property_IsCustomParse())
                ip.Property_Read(raw);
            else if (raw is TTable tbl)
                PopulateObject(tbl, created);
            return created;
        }
        catch (Exception e)
        {
            GLog.Warning($"TTable: skipped conversion to {member_type.Name} from {raw.GetType().Name} ({e.Message})");
            return null;
        }
    }

    public static object ToObject(TTable table, Type type)
    {
        Type _type = TClass<object>.Resolve(table.get_String("type")) ?? type;
        object obj = Activator.CreateInstance(_type)!;
        PopulateObject(table.get_Table("vars") ?? table, obj);

        return obj;
    }

    // =========================================================================
    // SELF-TEST
    // =========================================================================

    public struct SelfTest_Custom : I_Property
    {
        public string tag;
        public bool Property_IsCustomParse() => true;
        public void Property_Read(object value) => tag = value as string ?? "";
        public object Property_Write() => tag;
    }

    public class SelfTest_Obj
    {
        [ImpVar] public bool flag = true;
        [ImpVar] public float amount = 3.5f;
        [ImpVar] public string label = "hello";
        [ImpVar] public TTransform3 transform = new() { position = new Vector3(1, 2, 3), rotation = new Vector3(4, 5, 6), scale = new Vector3(7, 8, 9) };
        [ImpVar] public TFile file = new("{game}/foo.txt");
        [ImpVar] public SelfTest_Custom custom = new() { tag = "custom-parsed" };

        public SelfTest_Obj() { }
    }

    public static void SelfTest()
    {
        SelfTest_Obj original = new();

        void Check(SelfTest_Obj o, string via)
        {
            if (o.flag != original.flag
                || o.amount != original.amount
                || o.label != original.label
                || o.transform.position != original.transform.position
                || o.transform.rotation != original.transform.rotation
                || o.transform.scale != original.transform.scale
                || o.file.path != original.file.path
                || o.custom.tag != original.custom.tag)
                throw new Exception($"TTable.SelfTest: mismatch via {via}");
        }

        TTable table = FromObject(original);

        SelfTest_Obj from_json = (SelfTest_Obj)ToObject(FromJSON(ToJSON(table)), typeof(SelfTest_Obj));
        Check(from_json, "JSON");

        SelfTest_Obj from_toml = (SelfTest_Obj)ToObject(FromTOML(ToTOML(table)), typeof(SelfTest_Obj));
        Check(from_toml, "TOML");

        const string scene_toml = """
            type="A_Scene"
            prefab_refs={
                2526315="{game}/Scenes/prefab_test"
            }
            [vars]
            root={
                type="Imp3D",
                vars={},
                children=[
                    { type="C3_Mesh", vars={}, children={}, },
                    { type="C3_Mesh", vars={}, children={}, },
                    { is_prefab=true, prefab_id=2526315, vars={}, children={}, },
                ]
            }
            """;

        TTable scene = FromTOML(scene_toml);
        if (scene.get_String("type") != "A_Scene")
            throw new Exception("TTable.SelfTest: scene type");
        if (scene.get_Table("prefab_refs").get_String("2526315") != "{game}/Scenes/prefab_test")
            throw new Exception("TTable.SelfTest: prefab_refs");
        TTable root = scene.get_Table("vars.root");
        if (root.get_String("type") != "Imp3D")
            throw new Exception("TTable.SelfTest: root type");
        List<object> children = root.get_List("children");
        if (children.Count != 3 || children[0] is not TTable first || first.get_String("type") != "C3_Mesh")
            throw new Exception("TTable.SelfTest: children");
        if (children[2] is not TTable prefab || !prefab.get_Bool("is_prefab") || prefab.get_Int("prefab_id") != 2526315)
            throw new Exception("TTable.SelfTest: prefab node");

        TTable scene_roundtrip = FromTOML(ToTOML(scene));
        if (scene_roundtrip.get_List("vars.root.children").Count != 3)
            throw new Exception("TTable.SelfTest: scene TOML roundtrip");

        Console.WriteLine("TTable.SelfTest: PASS");
    }
}