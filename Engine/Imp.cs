using System.Numerics;
using System.Reflection;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using R3D_cs;
using Raylib_cs;
using Material = Raylib_cs.Material;

namespace Engine;

public struct TBuiltin
{
    public string key;
    public string name;
    public ImpAsset asset;
}

/*
 *  This is the main global function library
 */
[ImpClass(GlobalizeFunctions = true)]
public static class Imp
{
    public static App A;
    // =================================================================================================================
    // PATH
    // =================================================================================================================

    public static string Path_Normalize(string path)
    {
        string _pth = path;
        return _pth
                .Replace("//","/")
                .Replace("/","\\")
                .Replace("\\\\","\\")
            ;
    }

    public static string Path_Ascend(string directory, int dir_number)
    {
        if (string.IsNullOrEmpty(directory) || dir_number <= 0)
        {
            return directory;
        }

        DirectoryInfo currentDir = new DirectoryInfo(directory);
        
        for (int i = 0; i < dir_number; i++)
        {
            if (currentDir.Parent != null)
            {
                currentDir = currentDir.Parent;
            }
            else
            {
                break; // Stop if we reach the root directory
            }
        }

        return currentDir.FullName;
    }

    public static string GetDir_Root(EContentDir dir)
    {
        switch (dir)
        {
            case EContentDir.Game:
                return GetFileDir(App.game_file);
            case EContentDir.Engine:
                Console.WriteLine("ENIGNE ROOT IS:  "+AppContext.BaseDirectory);
#if DEBUG
                return Path_Ascend(AppContext.BaseDirectory,4);
#else
                return AppContext.BaseDirectory;
#endif
        }
        return "";
    }

    public static string GetDir_Content(EContentDir dir)
    {
        switch (dir)
        {
            case EContentDir.Game:
                return GetDir_Root(EContentDir.Game) + "\\Content\\";
            case EContentDir.Engine:
                return GetDir_Root(EContentDir.Engine) + "\\Content\\";
        }
        return "";
    }

    public static string Make_Path_Absolute(string path)
    {
        string output = Path_Normalize(path)
            .Replace("{game}", GetDir_Content(EContentDir.Game))
            .Replace("{engine}", GetDir_Content(EContentDir.Engine));
        return output;
    }

    public static string Make_Path_Local(string path)
    {
        string output = Path_Normalize(path)
            .Replace(GetDir_Content(EContentDir.Game),"{game}")
            .Replace(GetDir_Content(EContentDir.Engine),"{engine}");
        return output;
        
    }

    public static string GetExePath() { return Path_Normalize(System.Environment.ProcessPath); }

    public static string GetFileDir(string filepath)
    {
        string _path = "";
        var dirnam = Path.GetDirectoryName(filepath);
        if (dirnam != null) _path = Path_Normalize(dirnam);
        return _path;
    }

    // =================================================================================================================
    // FILE
    // =================================================================================================================
    
    public static string File_GetFirstOfExt(string dir, string extension)
    {
        if (!Directory.Exists(dir))
            return "";

        extension = extension.TrimStart('.');

        return Directory
            .GetFiles(dir, $"*.{extension}", SearchOption.TopDirectoryOnly)
            .FirstOrDefault() ?? "";
    }
    
    public static string File_LoadAs_String(string filepath)
    {
        string _path = Make_Path_Absolute(filepath);
        return File.ReadAllText(_path);
    }

    public static TByteBlock File_LoadAs_Bytes(string filepath) { return new (){ data = File.ReadAllBytes(Make_Path_Absolute(filepath))}; }

    public static Type GetType_FromExtension(string file)
    {
        string _ext = Path.GetExtension(file).TrimStart('.').ToUpper();
        return Type.GetType("Engine.Files.File_" + _ext);
    }

    private static ImpFile File_ImportInternal(string filepath, bool force = false, Type? type = null)
    {
        TFile pth = new(filepath);
        if (!force && App.files.TryGetValue(pth, out ImpFile existing)) return existing;

        type ??= Imp.GetType_FromExtension(filepath) ?? typeof(ImpFile);
        ImpFile _new_file = Activator.CreateInstance(type) as ImpFile;
        if (_new_file == null) return null;
        _new_file.filepath = filepath;
        _new_file.Reimport();
        App.files[pth] = _new_file;
        return _new_file;
    }

    public static T? File_Import<T>(string filepath, bool force=false) where T : ImpFile
    {
        Type type = typeof(T);
        if (type == typeof(ImpFile)) type = Imp.GetType_FromExtension(filepath) ?? typeof(ImpFile);
        return File_ImportInternal(filepath, force, type) as T;
    }

    public static void File_ImportAllInDir(string dir, bool force = false)
    {
        foreach (string file in Directory.GetFiles(dir))
        {
            Type? _type = Imp.GetType_FromExtension(file);
            if (_type != null)
            {
                File_ImportInternal(file, force, _type);
            }
        }
    }


    // =================================================================================================================
    // MOD
    // =================================================================================================================

    public static string Mod_GetDirRoot(string mod_name)
    {
        
        return "";
    }
    

    public static string Mod_GetFilePath(string mod_name, string file_name)
    {
        string _root= Mod_GetDirRoot(mod_name);
        string _ext = Path.GetExtension(file_name);
        
        string full_path = _root + "/"+ _ext + "/" + file_name;
        return full_path;
    }
    
    // =================================================================================================================
    // ASSETS
    // =================================================================================================================

    const string BUILTIN_PREFIX = "{builtin}/";
    static bool builtins_ready;
    static readonly List<TBuiltin> builtins = new();
    static readonly Dictionary<string, ImpAsset> builtins_by_key = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<ImpAsset> builtins_set = new();

    public static IReadOnlyList<TBuiltin> Builtin_All()
    {
        Builtin_Ensure();
        return builtins;
    }

    public static bool Builtin_Is(ImpAsset? asset)
    {
        if (asset == null) return false;
        Builtin_Ensure();
        return builtins_set.Contains(asset);
    }
    
    public static T? Asset_Load<T>(string filepath) where T : ImpAsset
    {
        return Asset_Load(filepath, typeof(T)) as T;
    }
    
    public static ImpAsset Asset_Load(string filepath, Type type = null)
    {
        TFile pth = new(filepath);
        if (App.assets.ContainsKey(pth)) return App.assets[pth];

        Builtin_Ensure();
        if (App.assets.ContainsKey(pth)) return App.assets[pth];
        if (!string.IsNullOrEmpty(filepath) && builtins_by_key.TryGetValue(filepath, out ImpAsset builtin))
            return builtin;

        if (!string.IsNullOrEmpty(filepath) &&
            (filepath.StartsWith(BUILTIN_PREFIX, StringComparison.OrdinalIgnoreCase) ||
             filepath.StartsWith("{builtin}\\", StringComparison.OrdinalIgnoreCase)))
        {
            Log_Error($"Could not find asset {filepath}");
            return null;
        }

        string abs = Imp.Make_Path_Absolute(filepath);
        if (string.IsNullOrEmpty(filepath) || !File.Exists(abs))
        {
            Log_Error($"Could not find asset {filepath}");
            return null;
        }

        TTable _tbl = TTable.FromTOML(Imp.File_LoadAs_String(filepath));
        Type _type = TClass<object>.Resolve(_tbl.get_String("type")) ?? type ?? typeof(ImpAsset);
        ImpAsset asset = Activator.CreateInstance(_type) as ImpAsset;
        if (asset == null) return null;
        App.assets.Add(pth, asset);
        asset.filepath = filepath;
        asset.From_Table(_tbl);
        asset.Source_Reimport();
        return asset;
    }
    
    //imports an ImpFile fromt a sourcefile and then creates OR loads an ImpAsset from the sourcefile.
    public static T? Asset_Import<T>(string sourcefile) where T : ImpAsset
    {
        Console.WriteLine(" --------------  ");
        if (string.IsNullOrEmpty(sourcefile)) return null;

        string abs_src = Make_Path_Absolute(sourcefile);
        if (!File.Exists(abs_src))
        {
            Log_Error($"Could not find sourcefile {sourcefile}");
            return null;
        }

        ImpFile file = File_Import<ImpFile>(sourcefile);
        if (file == null) return null;

        T asset = Activator.CreateInstance<T>();
        if (asset == null) return null;

        string asset_path = Path.ChangeExtension(sourcefile, asset.GetFileExtension());
        TFile pth = new(asset_path);
        if (App.assets.ContainsKey(pth)) return App.assets[pth] as T;

        if (File.Exists(Make_Path_Absolute(asset_path)))
            return Asset_Load<T>(asset_path);

        asset.sourcefile = file.filepath;
        asset.filepath = asset_path;
        App.assets[pth] = asset;
        asset.Source_Reimport();
        return asset;
    }
    
    static void Builtin_Ensure()
    {
        if (builtins_ready) return;
        builtins_ready = true;

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in types)
            {
                if (t == null || t.IsGenericTypeDefinition) continue;
                if ((t.Namespace ?? "").StartsWith("Editor_Vibe")) continue;

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    Collect(t, f, f.FieldType, () => f.GetValue(null));
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    Collect(t, p, p.PropertyType, () => p.GetValue(null));
                }
            }
        }

        builtins.Sort((a, b) =>
        {
            int c = string.Compare(a.asset.GetType().Name, b.asset.GetType().Name, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        void Collect(Type owner, MemberInfo member, Type member_type, Func<object?> get)
        {
            if (!typeof(ImpAsset).IsAssignableFrom(member_type)) return;
            BuiltinAttribute? attr = member.GetCustomAttribute<BuiltinAttribute>();
            if (attr == null) return;
            ImpAsset? asset;
            try { asset = get() as ImpAsset; }
            catch { return; }
            if (asset == null) return;

            string key = attr.Name;
            if (string.IsNullOrEmpty(key)) key = owner.Name + "." + member.Name;
            if (builtins_by_key.ContainsKey(key))
            {
                Log_Error($"Duplicate builtin '{key}' on {owner.Name}.{member.Name}");
                return;
            }

            if (string.IsNullOrEmpty(asset.filepath))
                asset.filepath = BUILTIN_PREFIX + key;

            string name = string.IsNullOrEmpty(attr.Name) ? member.Name : attr.Name;
            TBuiltin entry = new() { key = key, name = name, asset = asset };
            builtins.Add(entry);
            builtins_set.Add(asset);
            builtins_by_key[key] = asset;
            builtins_by_key[asset.filepath] = asset;
            App.assets[new TFile(asset.filepath)] = asset;
            App.assets[new TFile(key)] = asset;
        }
    }

    // =================================================================================================================
    // SAVE
    // =================================================================================================================
    
    
    // =================================================================================================================
    // SCENE
    // =================================================================================================================
    public static void Scene_Transit(TRef<A_Scene> scene, bool skip_fade = false)
    {
        
    }
    
    
    // ################################################################################################################
    // ################################################################################################################
    // MATH
    // ################################################################################################################
    // ################################################################################################################
    
    
    // -------------------------------------------------------------------------------------
    // Vector3
    // -------------------------------------------------------------------------------------
    // World basis: +X forward, +Y up, +Z left (right-handed).
    public static readonly Vector3 WORLD_UP = new(0, 1, 0);
    public static readonly Vector3 WORLD_FORWARD = new(1, 0, 0);
    public static readonly Vector3 WORLD_LEFT = new(0, 0, 1);

    public static Vector3 V3_Interp(Vector3 current, Vector3 target, double dt, double speed, bool constant = false)
    {
        if (speed <= 0 || dt <= 0)
            return current;

        if (constant)
        {
            Vector3 delta = target - current;
            float distance = delta.Length();
            float step = (float)(speed * dt);

            if (distance <= step || distance == 0f)
                return target;

            return current + delta / distance * step;
        }

        float alpha = 1f - (float)Math.Exp(-dt * speed);
        return Vector3.Lerp(current, target, alpha);
    }

    public static Vector3 V3_Average(List<Vector3> points)
    {
        if (points.Count == 0)
            return Vector3.Zero;

        return points.Aggregate((a, b) => a + b) / points.Count;
    }

    public static Vector3 V3_Combine(List<Vector3> vectors, bool normalize = false)
    {
        if (vectors.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 result = Vector3.Zero;
        for (int i = 0; i < vectors.Count; i++)
        {
            result += vectors[i];
        }

        if (normalize && result.LengthSquared() > 0f)
        {
            result = Vector3.Normalize(result);
        }

        return result;
    }

    public static Vector3 V3_Offset(Vector3 vector, Vector3 offset, Vector3 rotation)
    {
        return vector + Vector3.Transform(offset, (Quaternion)Euler_2_Quat(rotation));
    }

    public static Vector2 V3_to_V2(Vector3 v,bool spatial = false)
    {
        if(spatial) return new Vector2(v.X, v.Z);
        return new Vector2(v.X, v.Y);
    }

    public static Vector3 V3_Rotate(Vector3 vector, Vector3 rotation)
    {
        return Vector3.Transform(vector, (Quaternion)Euler_2_Quat(rotation));
    }

    public static Vector3 V3_LookAt(Vector3 start, Vector3 target)
    {
        Vector3 direction = target - start;

        if (direction.LengthSquared() <= float.Epsilon)
            return Vector3.Zero;

        direction = Vector3.Normalize(direction);

        float yaw = MathF.Atan2(direction.Z, direction.X);
        float horizontalLength = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        float pitch = MathF.Atan2(direction.Y, horizontalLength);

        float rad2deg = 180f / MathF.PI;
        return new Vector3(0f, pitch * rad2deg, yaw * rad2deg);
    }
    /*   
    public static void GetRotVectors(Vector3 rotation, out Vector3 forward, out Vector3 right, out Vector3 up, bool x=false, bool y=true, bool z=false)
    {
        
    }
   */ 
    
    // -------------------------------------------------------------------------------------
    // Vector2
    // -------------------------------------------------------------------------------------

    public static Vector2 V2_Interp(Vector2 current, Vector2 target, double dt, double speed, bool constant)
    {
        return Vector2.Lerp(current, target, (float)Math.Pow(Math.E, -dt * speed));
    }

    public static Vector3 V2_to_V3(Vector2 v, bool spatial = false)
    {
        if (spatial) return new Vector3(v.X, 0, v.Y);
        return new Vector3(v.X, v.Y, 0);
    }

    // -------------------------------------------------------------------------------------
    // Rotation
    // -------------------------------------------------------------------------------------
    
    // X=roll, Y=pitch, Z=yaw (degrees) — same convention as Imp3D.
    // Rotations compose roll -> pitch -> yaw about the Y-up basis above, so a zero rotation
    // faces WORLD_FORWARD. Positive pitch tilts the nose up, positive yaw turns left.

    public static Quaternion Euler_2_Quat(Vector3 euler_deg)
    {
        float deg2rad = MathF.PI / 180f;
        float roll = euler_deg.X * deg2rad;
        float pitch = euler_deg.Y * deg2rad;
        float yaw = -euler_deg.Z * deg2rad; // negated so +yaw is turn-left

        // Numerics' q1 * q2 applies q2 first, so this is yaw(pitch(roll(v))).
        return Quaternion.CreateFromAxisAngle(WORLD_UP, yaw)
               * Quaternion.CreateFromAxisAngle(WORLD_LEFT, pitch)
               * Quaternion.CreateFromAxisAngle(WORLD_FORWARD, roll);
    }

    public static Vector3 Quat_2_Euler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float rad2deg = 180f / MathF.PI;

        Vector3 fwd = Vector3.Transform(WORLD_FORWARD, q);
        Vector3 left = Vector3.Transform(WORLD_LEFT, q);
        Vector3 up = Vector3.Transform(WORLD_UP, q);

        float sin_pitch = Vector3.Dot(fwd, WORLD_UP);
        float pitch, yaw, roll;
        if (MathF.Abs(sin_pitch) >= 0.99999f)
        {
            // Straight up/down: roll and yaw share an axis, so fold everything into yaw.
            pitch = MathF.CopySign(MathF.PI / 2f, sin_pitch);
            yaw = MathF.Atan2(-Vector3.Dot(left, WORLD_FORWARD), Vector3.Dot(left, WORLD_LEFT));
            roll = 0f;
        }
        else
        {
            pitch = MathF.Asin(sin_pitch);
            yaw = MathF.Atan2(Vector3.Dot(fwd, WORLD_LEFT), Vector3.Dot(fwd, WORLD_FORWARD));
            roll = MathF.Atan2(-Vector3.Dot(left, WORLD_UP), Vector3.Dot(up, WORLD_UP));
        }
        return new Vector3(roll * rad2deg, pitch * rad2deg, yaw * rad2deg);
    }
    
    // ################################################################################################################
    // ################################################################################################################
    // PHYSICS
    // ################################################################################################################
    // ################################################################################################################
    
    
    // -------------------------------------------------------------------------------------
    // 2D
    // -------------------------------------------------------------------------------------

    //checks a point on screen for the first Imp2D Comp it finds.
    public static Imp2D Trace2D_ForComp(Vector2 position, ImpComp root)
    {
        Imp2D TraceNode(ImpComp n)
        {
            if (n == null || !n.is_visible) return null;
            Imp2D self = n as Imp2D;
            if (self != null && self.cursor_filter == ECursorFilter.Ignore) return null;

            for (int i = n.children.Count - 1; i >= 0; i--)
            {
                Imp2D child = TraceNode(n.children[i]);
                if (child != null) return child;
            }

            if (self != null && self.cursor_filter == ECursorFilter.Hit && self.Contains(position))
                return self;
            return null;
        }

        return TraceNode(root);
    }

    public static Imp2D Trace2D_ForComp(Vector2 position)
    {
        Imp2D hit = Trace2D_ForComp(position, App.scene_current?.root);
        if (hit != null) return hit;
        for (int i = App.scenes_global.Count - 1; i >= 0; i--)
        {
            hit = Trace2D_ForComp(position, App.scenes_global[i]?.root);
            if (hit != null) return hit;
        }
        return null;
    }
    
    // -------------------------------------------------------------------------------------
    // 3D
    // -------------------------------------------------------------------------------------
    private static R3D_cs.Mesh _line_mesh;
    private static bool _line_mesh_ready;

    public static TBounds3 Draw3D_Line(Vector3 start, Vector3 end, float thickness, Color color)
    {
        Vector3 delta = end - start;
        float len = delta.Length();
        if (len < 1e-6f)
        {
            return TBounds3.ZERO;
        }
        if (thickness < 0.001f)
        {
            thickness = 0.001f;
        }
        if (!_line_mesh_ready)
        {
            _line_mesh = R3D.GenMeshCylinder(0.5f, 1f, 8);
            _line_mesh.ShadowCastMode = ShadowCastMode.Disabled;
            _line_mesh_ready = true;
        }

        Vector3 dir = delta / len;
        Vector3 mid = (start + end) * 0.5f;
        Quaternion rot;
        float along = Vector3.Dot(Vector3.UnitY, dir);
        if (along > 0.9999f)
        {
            rot = Quaternion.Identity;
        }
        else if (along < -0.9999f)
        {
            rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        }
        else
        {
            Vector3 axis = Vector3.Cross(Vector3.UnitY, dir);
            rot = Quaternion.Normalize(new Quaternion(axis.X, axis.Y, axis.Z, 1f + along));
        }

        R3D_cs.Material mat = R3D.GetDefaultMaterial();
        AlbedoMap alb = mat.Albedo;
        alb.Color = color;
        mat.Albedo = alb;
        mat.Unlit = true;
        R3D.DrawMeshEx(_line_mesh, mat, mid, rot, new Vector3(thickness, len, thickness));
        return new TBounds3
        {
            center = mid,
            size = new Vector3(thickness, len, thickness),
            rotation = Imp.Quat_2_Euler(rot),
        };
    }

    public static TBounds3 Draw3D_Box(TTransform3 transform, Vector3 bounds, float thickness =0.4f, Color color = default)
    {
        if (color.A == 0)
        {
            color = Color.White;
        }
        Quaternion rot = Imp.Euler_2_Quat(transform.rotation);
        Vector3 h = new Vector3(
            MathF.Abs(bounds.X * transform.scale.X) * 0.5f,
            MathF.Abs(bounds.Y * transform.scale.Y) * 0.5f,
            MathF.Abs(bounds.Z * transform.scale.Z) * 0.5f);
        Vector3 c = transform.position;
        Vector3 P(float x, float y, float z)
        {
            return c + Vector3.Transform(new Vector3(x, y, z), rot);
        }
        Vector3 p000 = P(-h.X, -h.Y, -h.Z);
        Vector3 p001 = P(-h.X, -h.Y,  h.Z);
        Vector3 p010 = P(-h.X,  h.Y, -h.Z);
        Vector3 p011 = P(-h.X,  h.Y,  h.Z);
        Vector3 p100 = P( h.X, -h.Y, -h.Z);
        Vector3 p101 = P( h.X, -h.Y,  h.Z);
        Vector3 p110 = P( h.X,  h.Y, -h.Z);
        Vector3 p111 = P( h.X,  h.Y,  h.Z);
        Draw3D_Line(p000, p001, thickness, color);
        Draw3D_Line(p001, p101, thickness, color);
        Draw3D_Line(p101, p100, thickness, color);
        Draw3D_Line(p100, p000, thickness, color);
        Draw3D_Line(p010, p011, thickness, color);
        Draw3D_Line(p011, p111, thickness, color);
        Draw3D_Line(p111, p110, thickness, color);
        Draw3D_Line(p110, p010, thickness, color);
        Draw3D_Line(p000, p010, thickness, color);
        Draw3D_Line(p001, p011, thickness, color);
        Draw3D_Line(p101, p111, thickness, color);
        Draw3D_Line(p100, p110, thickness, color);
        return new TBounds3
        {
            center = c,
            size = h * 2f,
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Capsule(TTransform3 transform, float radius, float height, int slices = 16, Color color = default)
    {
        if (radius < 0.001f || height < 0.001f)
        {
            return TBounds3.ZERO;
        }
        if (color.A == 0)
        {
            color = Color.White;
        }
        if (slices < 4)
        {
            slices = 4;
        }
        Quaternion q = Imp.Euler_2_Quat(transform.rotation);
        float rx = MathF.Abs(transform.scale.X) * radius;
        float ry = MathF.Abs(transform.scale.Y) * radius;
        float rz = MathF.Abs(transform.scale.Z) * radius;
        float hy = MathF.Abs(transform.scale.Y) * height;
        if (hy < ry * 2f)
        {
            hy = ry * 2f;
        }
        float thick = Math.Clamp(MathF.Min(rx, rz) * 0.03f, 0.01f, 0.08f);
        Vector3 o = transform.position;
        Vector3 top_c = new Vector3(0f, hy * 0.5f - ry, 0f);
        Vector3 bot_c = new Vector3(0f, -hy * 0.5f + ry, 0f);
        DrawWireCircle(o, q, top_c, new Vector3(rx, 0f, 0f), new Vector3(0f, 0f, rz), slices, thick, color);
        DrawWireCircle(o, q, bot_c, new Vector3(rx, 0f, 0f), new Vector3(0f, 0f, rz), slices, thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(rx, 0f, 0f), top_c + new Vector3(rx, 0f, 0f), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(-rx, 0f, 0f), top_c + new Vector3(-rx, 0f, 0f), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(0f, 0f, rz), top_c + new Vector3(0f, 0f, rz), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(0f, 0f, -rz), top_c + new Vector3(0f, 0f, -rz), thick, color);
        int steps = slices / 2;
        if (steps < 4)
        {
            steps = 4;
        }

        DrawWireHemi(o, q, top_c, rx, ry, rz, 1f, steps, thick, color);
        DrawWireHemi(o, q, bot_c, rx, ry, rz, -1f, steps, thick, color);
        return new TBounds3
        {
            center = o,
            size = new Vector3(rx * 2f, hy, rz * 2f),
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Sphere(TTransform3 transform, float radius, int slices = 16, Color color = default)
    {
        if (radius < 0.001f) return TBounds3.ZERO;
        if (color.A == 0) color = Color.White;
        if (slices < 4) slices = 4;
        Quaternion q = Imp.Euler_2_Quat(transform.rotation);
        Vector3 r = new Vector3(
            MathF.Abs(transform.scale.X) * radius,
            MathF.Abs(transform.scale.Y) * radius,
            MathF.Abs(transform.scale.Z) * radius);
        float thick = Math.Clamp(MathF.Min(r.X, MathF.Min(r.Y, r.Z)) * 0.03f, 0.01f, 0.08f);
        Vector3 o = transform.position;
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(r.X, 0f, 0f), new Vector3(0f, r.Y, 0f), slices, thick, color);
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(r.X, 0f, 0f), new Vector3(0f, 0f, r.Z), slices, thick, color);
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(0f, r.Y, 0f), new Vector3(0f, 0f, r.Z), slices, thick, color);
        return new TBounds3
        {
            center = o,
            size = r * 2f,
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Arrow(TTransform3 transform, float length, float thickness=0.3f, Color color=default)
    {
        if (length < 0.001f)
        {
            return TBounds3.ZERO;
        }
        if (color.A == 0)
        {
            color = Color.White;
        }
        if (thickness < 0.001f)
        {
            thickness = 0.001f;
        }
        Quaternion q = Imp.Euler_2_Quat(transform.rotation);
        float len = length * MathF.Abs(transform.scale.Z);
        if (len < 0.001f)
        {
            return TBounds3.ZERO;
        }
        Vector3 o = transform.position;
        Vector3 tip = new Vector3(0f, 0f, -len);
        float head = len * 0.22f;
        float head_w = len * 0.1f;
        Vector3 hb = new Vector3(0f, 0f, -len + head);
        DrawWireLine(o, q, Vector3.Zero, tip, thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(-head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(0f, head_w, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(0f, -head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(head_w, 0f, 0f), hb + new Vector3(0f, head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(0f, head_w, 0f), hb + new Vector3(-head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(-head_w, 0f, 0f), hb + new Vector3(0f, -head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(0f, -head_w, 0f), hb + new Vector3(head_w, 0f, 0f), thickness, color);
        return new TBounds3
        {
            center = o + Vector3.Transform(new Vector3(0f, 0f, -len * 0.5f), q),
            size = new Vector3(head_w * 2f, head_w * 2f, len),
            rotation = transform.rotation,
        };
    }

    private static void DrawWireLine(Vector3 origin, Quaternion rot, Vector3 a, Vector3 b, float thickness, Color color)
    {
        Draw3D_Line(origin + Vector3.Transform(a, rot), origin + Vector3.Transform(b, rot), thickness, color);
    }

    private static void DrawWireCircle(Vector3 origin, Quaternion rot, Vector3 center, Vector3 axis_a, Vector3 axis_b, int slices, float thickness, Color color)
    {
        Vector3 prev = default;
        for (int i = 0; i <= slices; i++)
        {
            float t = (float)i / slices * MathF.PI * 2f;
            Vector3 lp = center + axis_a * MathF.Cos(t) + axis_b * MathF.Sin(t);
            Vector3 wp = origin + Vector3.Transform(lp, rot);
            if (i > 0)
            {
                Draw3D_Line(prev, wp, thickness, color);
            }
            prev = wp;
        }
    }

    private static void DrawWireHemi(Vector3 origin, Quaternion rot, Vector3 center, float rx, float ry, float rz, float y_sign, int steps, float thickness, Color color)
    {
        Vector3[] rad =
        {
            new Vector3(rx, 0f, 0f),
            new Vector3(-rx, 0f, 0f),
            new Vector3(0f, 0f, rz),
            new Vector3(0f, 0f, -rz),
        };
        for (int m = 0; m < 4; m++)
        {
            Vector3 prev = default;
            for (int i = 0; i <= steps; i++)
            {
                float a = (float)i / steps * MathF.PI * 0.5f;
                Vector3 lp = center + rad[m] * MathF.Cos(a) + new Vector3(0f, y_sign * ry * MathF.Sin(a), 0f);
                Vector3 wp = origin + Vector3.Transform(lp, rot);
                if (i > 0)
                {
                    Draw3D_Line(prev, wp, thickness, color);
                }
                prev = wp;
            }
        }
    }

    public static TBounds3 Draw3D_Billboard(A_Texture billboard, TTransform3 transform, float size=1.0f, Color color = default)
    {
        Raylib.DrawBillboard(App.view_target_data,billboard.texture, transform.position, size, color);
        return default;
    }
    
    // ---------------------------------------------------------------------------------------------------
    // Trace
    // ---------------------------------------------------------------------------------------------------

    public static TTraceResult3D Trace3D_Line(Vector3 start, Vector3 end, ECollisionChannel channel,
        Func<Imp3D, bool> filter = null)
    {
        return default;
    }

    public static bool Ray_Plane(JoltPhysicsSharp.Ray ray, Vector3 plane_p, Vector3 plane_n, out Vector3 hit)
    {
        hit = default;
        float denom = Vector3.Dot(ray.Direction, plane_n);
        if (MathF.Abs(denom) < 1e-7f) return false;
        float t = Vector3.Dot(plane_p - ray.Position, plane_n) / denom;
        if (t < 0f) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static bool Ray_AABB(JoltPhysicsSharp.Ray ray, Vector3 min, Vector3 max, out float t)
    {
        t = 0f;
        Vector3 inv = new(
            MathF.Abs(ray.Direction.X) > 1e-12f ? 1f / ray.Direction.X : 1e12f,
            MathF.Abs(ray.Direction.Y) > 1e-12f ? 1f / ray.Direction.Y : 1e12f,
            MathF.Abs(ray.Direction.Z) > 1e-12f ? 1f / ray.Direction.Z : 1e12f);
        float t1 = (min.X - ray.Position.X) * inv.X;
        float t2 = (max.X - ray.Position.X) * inv.X;
        float t3 = (min.Y - ray.Position.Y) * inv.Y;
        float t4 = (max.Y - ray.Position.Y) * inv.Y;
        float t5 = (min.Z - ray.Position.Z) * inv.Z;
        float t6 = (max.Z - ray.Position.Z) * inv.Z;
        float tmin = MathF.Max(MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4)), MathF.Min(t5, t6));
        float tmax = MathF.Min(MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4)), MathF.Max(t5, t6));
        if (tmax < 0f || tmin > tmax) return false;
        t = tmin >= 0f ? tmin : tmax;
        return t >= 0f;
    }

    public static bool Ray_Comp3D(JoltPhysicsSharp.Ray ray, Imp3D c, out float t, out Vector3 hit)
    {
        t = 0f;
        hit = default;
        if (c == null || !c.is_visible)
        {
            return false;
        }
        TBounds3 b = c.bounds;
        if (b.IsEmpty)
        {
            return false;
        }
        Quaternion q = Imp.Euler_2_Quat(b.rotation);
        Quaternion inv_q = Quaternion.Inverse(q);
        Vector3 h = new(
            MathF.Abs(b.size.X) * 0.5f,
            MathF.Abs(b.size.Y) * 0.5f,
            MathF.Abs(b.size.Z) * 0.5f);
        Vector3 o = Vector3.Transform(ray.Position - b.center, inv_q);
        Vector3 d = Vector3.Transform(ray.Direction, inv_q);
        if (!Ray_AABB(new JoltPhysicsSharp.Ray(o, d), -h, h, out t))
        {
            return false;
        }
        hit = ray.Position + ray.Direction * t;
        return true;
    }
    
    // =================================================================================================================
    // LOG
    // =================================================================================================================
    
    public static void Log(string msg, bool on_screen, Color color=default)
    {
        Console.WriteLine(msg);
        Console.Out.Flush();
    }
    
    [ScriptCall] public static void Log_Info(string msg,bool on_screen=false) { Log(msg, on_screen); }
    [ScriptCall] public static void Log_Warning(string msg,bool on_screen=false) { Log("WARNING: "+msg, on_screen,Color.Yellow); }
    [ScriptCall] public static void Log_Error(string msg,bool on_screen=false) { Log("ERROR: "+msg, on_screen,Color.Red); }
}