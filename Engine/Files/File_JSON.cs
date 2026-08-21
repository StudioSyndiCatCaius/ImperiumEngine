using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Files;

public class File_JSON : ImpFile
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public JsonObject? root;
    public string? ClassName => root?["_class"]?.GetValue<string>();

    public bool Parse()
    {
        if (root != null) return true;
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath)) return false;
        try { root = JsonNode.Parse(File.ReadAllText(filepath)) as JsonObject; }
        catch { return false; }
        return root != null;
    }

    public override bool File_Read(object target)
    {
        if (!Parse() || root == null) return false;
        if (target is ImpAsset asset)
        {
            if (root["vars"] is not JsonObject vars) return false;
            ApplyVars(asset, vars, asset.filepath);
            if (asset is ImpScene scene && vars["root"] is JsonNode root_node)
            {
                ImpComp? loaded = Comp_FromJson(root_node, asset.filepath);
                if (loaded != null) scene.root = loaded;
            }
            return true;
        }
        ApplyFields(target, root, null);
        return true;
    }

    public override bool File_Write(object target)
    {
        if (string.IsNullOrEmpty(filepath)) return false;
        try
        {
            string? dir = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);
            JsonNode? node;
            if (target is ImpAsset asset)
            {
                visiting.Add(asset);
                JsonObject vars = Vars_ToJson(asset, visiting);
                if (asset is ImpScene scene) vars["root"] = Comp_ToJson(scene.root, visiting);
                node = new JsonObject
                {
                    ["_class"] = asset.GetType().Name,
                    ["vars"] = vars,
                };
            }
            else
            {
                node = ToJson(target, visiting);
            }
            if (node == null) return false;
            File.WriteAllText(filepath, node.ToJsonString(WriteOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    static IEnumerable<FieldInfo> ImpVarFields(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.IsDefined(typeof(ImpVarAttribute), true));
    }

    static FieldInfo[] PublicFields(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Instance);
    }

    static JsonObject Vars_ToJson(ImpAsset asset, HashSet<object> visiting)
    {
        var vars = new JsonObject();
        foreach (FieldInfo f in ImpVarFields(asset.GetType()))
        {
            ImpVarAttribute attr = f.GetCustomAttribute<ImpVarAttribute>()!;
            string key = string.IsNullOrEmpty(attr.Name) ? f.Name : attr.Name;
            vars[key] = ToJson(f.GetValue(asset), visiting);
        }
        vars["source_file"] = SourceFile_ToJson(asset);
        vars["source_index"] = asset.source_index;
        return vars;
    }

    static void ApplyVars(ImpAsset asset, JsonObject vars, string? path_context)
    {
        string? ctx = string.IsNullOrEmpty(asset.filepath) ? path_context : asset.filepath;

        if (vars.ContainsKey("source_file"))
        {
            JsonNode? n = vars["source_file"];
            if (n == null || n.GetValueKind() == JsonValueKind.Null)
                asset.source_file = null;
            else
            {
                string src = n.GetValue<string>();
                asset.source_file = string.IsNullOrEmpty(src)
                    ? null
                    : ImpFile.GetOrCreate(Path_ResolveRef(ctx, src));
            }
        }
        if (vars["source_index"] is JsonValue idx && idx.TryGetValue<int>(out int i))
            asset.source_index = i;

        foreach (FieldInfo f in ImpVarFields(asset.GetType()))
        {
            ImpVarAttribute attr = f.GetCustomAttribute<ImpVarAttribute>()!;
            string key = string.IsNullOrEmpty(attr.Name) ? f.Name : attr.Name;
            if (key == "source_file" || key == "source_index") continue;
            if (!vars.ContainsKey(key)) continue;
            object? val = FromJson(vars[key], f.FieldType, ctx);
            if (val != null || !f.FieldType.IsValueType)
                f.SetValue(asset, val);
        }
    }

    static void ApplyFields(object target, JsonObject obj, string? path_context)
    {
        foreach (FieldInfo f in PublicFields(target.GetType()))
        {
            if (!obj.ContainsKey(f.Name)) continue;
            if (IsHandleType(f.FieldType) || typeof(ImpComp).IsAssignableFrom(f.FieldType)) continue;
            object? val = FromJson(obj[f.Name], f.FieldType, path_context);
            if (val != null || !f.FieldType.IsValueType)
                f.SetValue(target, val);
        }
    }

    static JsonNode? SourceFile_ToJson(ImpAsset asset)
    {
        if (asset.source_file == null || string.IsNullOrEmpty(asset.source_file.filepath)) return null;
        string resolved = ImpFile.Path_Resolve(asset.source_file.filepath);
        if (!string.IsNullOrEmpty(asset.filepath))
        {
            string? dir = Path.GetDirectoryName(ImpFile.Path_Resolve(asset.filepath));
            if (!string.IsNullOrEmpty(dir))
            {
                try
                {
                    string rel = Path.GetRelativePath(dir, Path.GetFullPath(resolved));
                    if (!rel.StartsWith("..") && !Path.IsPathRooted(rel))
                        return JsonValue.Create(rel.Replace('\\', '/'));
                }
                catch { }
            }
        }
        return JsonValue.Create(Path_Tokenize(resolved));
    }

    /// <summary>
    /// Turns a reference string into a full path. A reference is tokenized, absolute, or
    /// relative to the file holding it (asset_path).
    /// </summary>
    public static string Path_ResolveRef(string? asset_path, string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        if (source.Contains("{engine}") || source.Contains("{game}"))
            return ImpFile.Path_Resolve(source);
        string resolved = ImpFile.Path_Resolve(source);
        if (Path.IsPathRooted(resolved)) return resolved;
        if (!string.IsNullOrEmpty(asset_path))
        {
            string? dir = Path.GetDirectoryName(ImpFile.Path_Resolve(asset_path));
            if (!string.IsNullOrEmpty(dir))
                return Path.GetFullPath(Path.Combine(dir, source));
        }
        try { return Path.GetFullPath(resolved); }
        catch { return resolved; }
    }

    static bool Path_IsUnder(string path, string root)
    {
        try
        {
            string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return p.Equals(r, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(r + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Writes a full path back in portable "{engine}/..." / "{game}/..." form where it can.</summary>
    public static string Path_Tokenize(string path)
    {
        string real;
        try { real = Path.GetFullPath(ImpFile.Path_Resolve(path)); }
        catch { real = ImpFile.Path_Resolve(path); }

        string engine = ImpFile.ContentDir_Engine();
        if (Path_IsUnder(real, engine))
        {
            return Path_TokenRoot("{engine}", engine, real);
        }

        string game = ImpFile.ContentDir_Game();
        if (Path_IsUnder(real, game))
        {
            return Path_TokenRoot("{game}", game, real);
        }

        return path.Replace('\\', '/');
    }

    static string Path_TokenRoot(string token, string root, string real)
    {
        string rel = Path.GetRelativePath(root, real).Replace('\\', '/');
        if (string.IsNullOrEmpty(rel) || rel == ".")
        {
            return token;
        }
        return token + "/" + rel;
    }

    static string Path_WriteAsset(string asset_path)
    {
        if (ImpAsset.Path_IsBuiltin(asset_path)) return asset_path.Replace('\\', '/');
        if (asset_path.Contains("{engine}") || asset_path.Contains("{game}"))
            return asset_path.Replace('\\', '/');
        return Path_Tokenize(asset_path);
    }

    static bool IsHandleType(Type t)
    {
        return t == typeof(Texture2D) || t == typeof(Font) || t == typeof(Sound) || t == typeof(Model)
            || t.Name is "Mesh" or "Material" or "Light" or "Environment";
    }

    static double JsonNumber(JsonNode node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out double d)) return d;
            if (jv.TryGetValue<string>(out string? s)
                && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
        }
        return 0;
    }

    static string? JsonPath(JsonNode? node)
    {
        if (node == null || node.GetValueKind() == JsonValueKind.Null) return null;
        if (node.GetValueKind() == JsonValueKind.String) return node.GetValue<string>();
        if (node is JsonObject o && o["path"] != null && o["path"]!.GetValueKind() == JsonValueKind.String)
            return o["path"]!.GetValue<string>();
        return null;
    }

    static object? KeyFromString(string s, Type key_type)
    {
        if (key_type == typeof(string)) return s;
        if (key_type.IsEnum) return Enum.Parse(key_type, s, true);
        if (key_type.IsPrimitive) return Convert.ChangeType(s, key_type, CultureInfo.InvariantCulture);
        return s;
    }

    static JsonNode? ToJson(object? value, HashSet<object> visiting)
    {
        if (value == null) return null;

        Type type = value.GetType();
        if (IsHandleType(type) || typeof(ImpComp).IsAssignableFrom(type)) return null;

        switch (value)
        {
            case bool b: return JsonValue.Create(b);
            case string s: return JsonValue.Create(s);
            case byte n: return JsonValue.Create(n);
            case sbyte n: return JsonValue.Create(n);
            case short n: return JsonValue.Create(n);
            case ushort n: return JsonValue.Create(n);
            case int n: return JsonValue.Create(n);
            case uint n: return JsonValue.Create(n);
            case long n: return JsonValue.Create(n);
            case ulong n: return JsonValue.Create(n);
            case float n: return JsonValue.Create(n);
            case double n: return JsonValue.Create(n);
            case decimal n: return JsonValue.Create(n);
            case Enum: return JsonValue.Create(value.ToString());
            case ImpFile file:
                return string.IsNullOrEmpty(file.filepath) ? null : JsonValue.Create(Path_Tokenize(file.filepath));
            case ImpAsset asset:
                if (!string.IsNullOrEmpty(asset.filepath))
                    return new JsonObject { ["path"] = Path_WriteAsset(asset.filepath) };
                if (!visiting.Add(asset)) return null;
                var inline = new JsonObject
                {
                    ["_class"] = asset.GetType().Name,
                    ["vars"] = Vars_ToJson(asset, visiting),
                };
                visiting.Remove(asset);
                return inline;
        }

        if (value is IDictionary dict)
        {
            var obj = new JsonObject();
            foreach (DictionaryEntry e in dict)
            {
                if (e.Key == null) continue;
                string key = e.Key is Enum ? e.Key.ToString()! : Convert.ToString(e.Key, CultureInfo.InvariantCulture) ?? "";
                obj[key] = ToJson(e.Value, visiting);
            }
            return obj;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var arr = new JsonArray();
            foreach (object? item in enumerable)
                arr.Add(ToJson(item, visiting));
            return arr;
        }

        if (value is ImpFlowNode flow_node)
        {
            if (!visiting.Add(flow_node)) return null;
            var fn_obj = new JsonObject();
            fn_obj["_class"] = flow_node.GetType().Name;
            foreach (FieldInfo f in PublicFields(flow_node.GetType()))
            {
                if (f.Name == "_owner" || f.Name == "_player" || f.Name == "on_exit") continue;
                if (IsHandleType(f.FieldType) || typeof(ImpComp).IsAssignableFrom(f.FieldType)) continue;
                fn_obj[f.Name] = ToJson(f.GetValue(flow_node), visiting);
            }
            visiting.Remove(flow_node);
            return fn_obj;
        }

        if (type.IsValueType || type.IsClass)
        {
            FieldInfo[] fields = PublicFields(type);
            if (fields.Length == 0 && type.IsValueType)
                return JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture));

            if (!type.IsValueType && !visiting.Add(value)) return null;
            var obj = new JsonObject();
            foreach (FieldInfo f in fields)
            {
                if (IsHandleType(f.FieldType) || typeof(ImpComp).IsAssignableFrom(f.FieldType)) continue;
                obj[f.Name] = ToJson(f.GetValue(value), visiting);
            }
            if (!type.IsValueType) visiting.Remove(value);
            return obj;
        }

        return JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    static object? FromJson(JsonNode? node, Type type, string? path_context)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (node == null || node.GetValueKind() == JsonValueKind.Null)
        {
            if (type.IsValueType) return Activator.CreateInstance(type);
            return null;
        }

        if (type == typeof(string))
            return node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : node.ToJsonString();
        if (type == typeof(bool) && node is JsonValue jb && jb.TryGetValue<bool>(out bool bv))
            return bv;
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return Convert.ChangeType(JsonNumber(node), type, CultureInfo.InvariantCulture);
        if (type.IsEnum)
        {
            if (node.GetValueKind() == JsonValueKind.String)
                return Enum.Parse(type, node.GetValue<string>(), true);
            return Enum.ToObject(type, Convert.ToInt32(JsonNumber(node)));
        }
        if (typeof(ImpFile).IsAssignableFrom(type))
        {
            string? p = JsonPath(node);
            if (string.IsNullOrEmpty(p)) return null;
            return ImpFile.GetOrCreate(Path_ResolveRef(path_context, p));
        }
        if (typeof(ImpAsset).IsAssignableFrom(type))
        {
            if (node is JsonValue jv && jv.TryGetValue<string>(out string? sp) && !string.IsNullOrEmpty(sp))
                return ImpAsset.Load(sp);
            if (node is JsonObject o)
            {
                if (o["path"] != null)
                {
                    string? p = o["path"]!.GetValueKind() == JsonValueKind.String ? o["path"]!.GetValue<string>() : null;
                    return string.IsNullOrEmpty(p) ? null : ImpAsset.Load(p);
                }
                if (o["_class"] != null || o["vars"] != null)
                {
                    Type inst_type = type;
                    string? named = o["_class"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(named))
                    {
                        Type? found = ImpAsset.AssetType_FromName(named);
                        if (found != null && type.IsAssignableFrom(found)) inst_type = found;
                    }
                    if (inst_type.IsAbstract || Activator.CreateInstance(inst_type) is not ImpAsset inst)
                        return null;
                    if (o["vars"] is JsonObject v)
                    {
                        ApplyVars(inst, v, path_context);
                        inst.BindSource(false);
                    }
                    return inst;
                }
            }
            return null;
        }

        if (type.IsGenericType)
        {
            Type gen = type.GetGenericTypeDefinition();
            if (gen == typeof(Dictionary<,>) && node is JsonObject dobj)
            {
                Type kT = type.GetGenericArguments()[0];
                Type vT = type.GetGenericArguments()[1];
                IDictionary dict = (IDictionary)Activator.CreateInstance(type)!;
                foreach (var kv in dobj)
                {
                    object? key = KeyFromString(kv.Key, kT);
                    if (key == null) continue;
                    dict.Add(key, FromJson(kv.Value, vT, path_context));
                }
                return dict;
            }
            if (gen == typeof(List<>) && node is JsonArray larr)
            {
                Type eT = type.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(type)!;
                foreach (JsonNode? item in larr)
                    list.Add(FromJson(item, eT, path_context));
                return list;
            }
        }

        if (type.IsArray && node is JsonArray aarr)
        {
            Type eT = type.GetElementType()!;
            Array a = Array.CreateInstance(eT, aarr.Count);
            for (int i = 0; i < aarr.Count; i++)
                a.SetValue(FromJson(aarr[i], eT, path_context), i);
            return a;
        }

        if (node.GetValueKind() == JsonValueKind.String)
        {
            ConstructorInfo? ctor = type.GetConstructor(new[] { typeof(string) });
            if (ctor != null) return ctor.Invoke(new object[] { node.GetValue<string>() });
        }

        if (typeof(ImpFlowNode).IsAssignableFrom(type) && node is JsonObject fn_obj)
        {
            Type inst_type = type;
            string? named = fn_obj["_class"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(named))
            {
                Type? found = ImpFlowNode.Type_FromName(named);
                if (found != null && type.IsAssignableFrom(found)) inst_type = found;
            }
            if (inst_type.IsAbstract)
            {
                return null;
            }
            object? created = Activator.CreateInstance(inst_type);
            if (created is not ImpFlowNode inst)
            {
                return null;
            }
            ApplyFields(inst, fn_obj, path_context);
            return inst;
        }

        object boxed = Activator.CreateInstance(type)!;
        if (node is JsonObject fo)
            ApplyFields(boxed, fo, path_context);
        return boxed;
    }

    public static JsonObject? Comp_ToJson(ImpComp? comp)
    {
        if (comp == null) return null;
        return Comp_ToJson(comp, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    static JsonObject Comp_ToJson(ImpComp comp, HashSet<object> visiting)
    {
        var vars = new JsonObject();
        foreach (FieldInfo f in ImpVarFields(comp.GetType()))
        {
            ImpVarAttribute attr = f.GetCustomAttribute<ImpVarAttribute>()!;
            string key = string.IsNullOrEmpty(attr.Name) ? f.Name : attr.Name;
            if (key == "name") continue;
            if (typeof(ImpComp).IsAssignableFrom(f.FieldType)) continue;
            vars[key] = ToJson(f.GetValue(comp), visiting);
        }

        var children = new JsonArray();
        if (!comp.IsInstanceRoot)
        {
            for (int i = 0; i < comp.children.Count; i++)
            {
                ImpComp child = comp.children[i];
                if (child.IsPackedForeign) continue;
                if (child.IsOwned) continue;
                children.Add(Comp_ToJson(child, visiting));
            }
        }

        var owned = new JsonObject();
        FieldInfo[] owned_fields = ImpComp.OwnedFields(comp.GetType());
        for (int i = 0; i < owned_fields.Length; i++)
        {
            FieldInfo f = owned_fields[i];
            if (f.GetValue(comp) is not ImpComp slot)
            {
                continue;
            }
            if (slot.parent != comp)
            {
                continue;
            }
            owned[f.Name] = Comp_ToJson(slot, visiting);
        }

        var obj = new JsonObject
        {
            ["_class"] = comp.GetType().Name,
            ["name"] = comp.name ?? "",
            ["vars"] = vars,
            ["children"] = children,
        };
        if (owned.Count > 0)
        {
            obj["owned"] = owned;
        }
        if (comp.IsInstanceRoot && !string.IsNullOrEmpty(comp.packed.path))
            obj["instance"] = new JsonObject { ["path"] = Path_WriteAsset(comp.packed.path) };
        return obj;
    }

    public static ImpComp? Comp_FromJson(JsonNode? node, string? path_context)
    {
        if (node is not JsonObject obj) return null;
        string? instance_path = JsonPath(obj["instance"]);
        if (!string.IsNullOrEmpty(instance_path))
            instance_path = Path_ResolveRef(path_context, instance_path);

        ImpComp? comp = null;
        if (!string.IsNullOrEmpty(instance_path))
        {
            ImpScene? packed = ImpAsset.Load<ImpScene>(instance_path);
            comp = packed?.Instantiate();
            if (comp == null)
            {
                string? cls = obj["_class"]?.GetValue<string>();
                Type type = ImpComp.Type_FromName(cls) ?? typeof(ImpComp);
                if (type.IsAbstract || Activator.CreateInstance(type) is not ImpComp placeholder)
                    return null;
                placeholder.packed = new TRef<ImpScene>(instance_path);
                placeholder.packed_from = placeholder;
                comp = placeholder;
            }
        }
        else
        {
            string? cls = obj["_class"]?.GetValue<string>();
            Type type = ImpComp.Type_FromName(cls) ?? typeof(ImpComp);
            if (type.IsAbstract || Activator.CreateInstance(type) is not ImpComp created)
                return null;
            comp = created;
        }

        if (!comp.IsInstanceRoot)
        {
            comp.Owned_Bind();
        }
        Comp_ApplyJson(comp, obj, path_context, !comp.IsInstanceRoot);
        return comp;
    }

    static void Comp_ApplyJson(ImpComp comp, JsonObject obj, string? path_context, bool apply_tree)
    {
        if (obj["name"] is JsonValue jn && jn.TryGetValue<string>(out string? named) && named != null)
            comp.name = named;

        if (obj["vars"] is JsonObject vars)
        {
            foreach (FieldInfo f in ImpVarFields(comp.GetType()))
            {
                ImpVarAttribute attr = f.GetCustomAttribute<ImpVarAttribute>()!;
                string key = string.IsNullOrEmpty(attr.Name) ? f.Name : attr.Name;
                if (typeof(ImpComp).IsAssignableFrom(f.FieldType)) continue;
                if (!vars.ContainsKey(key)) continue;
                object? val = FromJson(vars[key], f.FieldType, path_context);
                if (val != null || !f.FieldType.IsValueType)
                    f.SetValue(comp, val);
            }
        }

        if (!apply_tree)
        {
            return;
        }

        if (obj["owned"] is JsonObject owned)
        {
            FieldInfo[] fields = ImpComp.OwnedFields(comp.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!owned.ContainsKey(f.Name) || owned[f.Name] is not JsonObject slot_obj)
                {
                    continue;
                }
                if (f.GetValue(comp) is ImpComp native)
                {
                    Comp_ApplyJson(native, slot_obj, path_context, true);
                    continue;
                }
                ImpComp? made = Comp_FromJson(slot_obj, path_context);
                if (made == null)
                {
                    continue;
                }
                f.SetValue(comp, made);
                comp.Child_Add(made);
            }
        }

        if (obj["children"] is JsonArray arr)
        {
            foreach (JsonNode? child in arr)
            {
                if (Comp_TryMergeOwned(comp, child, path_context))
                {
                    continue;
                }
                ImpComp? c = Comp_FromJson(child, path_context);
                if (c != null) comp.Child_Add(c);
            }
        }
    }

    static bool Comp_TryMergeOwned(ImpComp host, JsonNode? node, string? path_context)
    {
        if (host == null || node is not JsonObject obj)
        {
            return false;
        }
        string? child_name = null;
        if (obj["name"] is JsonValue jn && jn.TryGetValue<string>(out string? named))
        {
            child_name = named;
        }
        string? cls = obj["_class"]?.GetValue<string>();
        Type? type = ImpComp.Type_FromName(cls);
        FieldInfo[] fields = ImpComp.OwnedFields(host.GetType());
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = fields[i];
            if (f.GetValue(host) is not ImpComp native)
            {
                continue;
            }
            bool type_hit = type != null && type == native.GetType();
            if (!type_hit)
            {
                continue;
            }
            bool name_hit = string.Equals(child_name, f.Name, StringComparison.Ordinal)
                || string.Equals(child_name, native.name, StringComparison.Ordinal)
                || string.Equals(child_name, native.GetType().Name, StringComparison.Ordinal);
            if (!name_hit)
            {
                continue;
            }
            Comp_ApplyJson(native, obj, path_context, true);
            return true;
        }
        return false;
    }
}
