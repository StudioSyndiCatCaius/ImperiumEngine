using System.Globalization;
using System.Numerics;
using System.Reflection;
using Editor.Scenes;
using Editor.Windows;
using Engine.Assets;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using Raylib_cs;

namespace Editor;

public static class EdConfig
{
    public static List<TTable> pending_scenes = new();
    public static string pending_active = "";
    public static bool restoring;

    public static string FilePath()
    {
        string root = GFile.GetDir_Root(EContentDir.Game);
        if (string.IsNullOrEmpty(root)) return "";
        return Path.Combine(root, "Config", "Editor.toml");
    }

    public static void Load(SNC_Editor_Root root)
    {
        pending_scenes.Clear();
        pending_active = "";
        string path = FilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        TTable tbl;
        try { tbl = TTable.FromTOML(File.ReadAllText(path)); }
        catch (Exception e)
        {
            GLog.Warning("Editor.toml load failed: " + e.Message);
            return;
        }

        Walk(root, "", tbl, false);
        ApplyEditorVars(root.wnd_config_editor, tbl);
        pending_active = tbl.get_String("active_scene");
        foreach (object item in tbl.get_List("open_scenes"))
        {
            if (item is TTable t) pending_scenes.Add(t);
        }
    }

    public static void Save(SNC_Editor_Root root)
    {
        if (root == null) return;
        string path = FilePath();
        if (string.IsNullOrEmpty(path)) return;

        TTable tbl = new();
        Walk(root, "", tbl, true);
        StoreEditorVars(root.wnd_config_editor, tbl);

        List<object> scenes = new();
        WND_Scene wnd = root.wnd_scene;
        string active = "";
        for (int i = 0; i < wnd.scene_tabs.Count; i++)
        {
            EUI_SceneTab tab = wnd.scene_tabs[i];
            if (tab.scene == null || string.IsNullOrEmpty(tab.scene.filepath)) continue;
            TTable s = new();
            s.Set("path", tab.scene.filepath);
            s.Set("view", tab.view.ToString());
            Camera3D c3 = tab.viewport3d.camera;
            s.Set("cam3d_position", Vec(c3.Position));
            s.Set("cam3d_target", Vec(c3.Target));
            s.Set("cam3d_up", Vec(c3.Up));
            s.Set("cam3d_fov", c3.FovY);
            s.Set("cam3d_proj", c3.Projection.ToString());
            s.Set("look_dist", tab.viewport3d.look_dist);
            Camera2D c2 = tab.viewport2d.camera;
            s.Set("cam2d_target", Vec(c2.Target));
            s.Set("cam2d_zoom", c2.Zoom);
            s.Set("cam2d_rot", c2.Rotation);
            scenes.Add(s);
            if (tab == wnd.current_scene_tab) active = tab.scene.filepath;
        }
        tbl.Set("open_scenes", scenes);
        tbl.Set("active_scene", active);

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, TTable.ToTOML(tbl));
    }

    public static bool RestoreScenes(WND_Scene wnd)
    {
        if (pending_scenes.Count == 0) return false;
        restoring = true;
        EUI_SceneTab? active = null;
        try
        {
            for (int i = 0; i < pending_scenes.Count; i++)
            {
                TTable s = pending_scenes[i];
                string path = s.get_String("path");
                if (string.IsNullOrEmpty(path)) continue;
                A_Scene? scene = GAsset.Asset_Load<A_Scene>(path);
                if (scene == null) continue;
                wnd.Scene_Open(scene);
                EUI_SceneTab? tab = wnd.current_scene_tab;
                if (tab == null) continue;
                ApplyCam(tab, s);
                if (string.Equals(path, pending_active, StringComparison.OrdinalIgnoreCase))
                    active = tab;
            }
        }
        finally
        {
            restoring = false;
            pending_scenes.Clear();
        }
        if (wnd.scene_tabs.Count == 0) return false;
        if (active != null) wnd.current_scene_tab = active;
        wnd.BindPanels();
        return true;
    }

    public static void SaveEditorVars(WND_ConfigEditor wnd)
    {
        if (wnd == null || restoring) return;
        string path = FilePath();
        if (string.IsNullOrEmpty(path)) return;
        TTable tbl = new();
        if (File.Exists(path))
        {
            try { tbl = TTable.FromTOML(File.ReadAllText(path)); }
            catch (Exception e)
            {
                GLog.Warning("Editor.toml load failed: " + e.Message);
                tbl = new();
            }
        }
        StoreEditorVars(wnd, tbl);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, TTable.ToTOML(tbl));
    }

    static void StoreEditorVars(WND_ConfigEditor wnd, TTable tbl)
    {
        if (wnd == null) return;
        TTable vars = TTable.FromConfig(typeof(WND_ConfigEditor), wnd, false);
        TTable stat = TTable.FromConfig(typeof(WND_ConfigEditor), null, true);
        foreach (var pair in stat.data)
            vars.data[pair.Key] = pair.Value;
        if (vars.data.Count == 0) tbl.Remove("WND_ConfigEditor");
        else tbl.Set("WND_ConfigEditor", vars);
    }

    static void ApplyEditorVars(WND_ConfigEditor wnd, TTable tbl)
    {
        if (wnd == null) return;
        TTable vars = tbl.get_Table("WND_ConfigEditor");
        if (vars == null) return;
        TTable.PopulateConfig(vars, typeof(WND_ConfigEditor), wnd, false);
        TTable.PopulateConfig(vars, typeof(WND_ConfigEditor), null, true);
    }

    static void ApplyCam(EUI_SceneTab tab, TTable s)
    {
        string view = s.get_String("view");
        if (!string.IsNullOrEmpty(view))
        {
            if (Enum.TryParse(view, true, out ESceneEditView v))
                tab.view = v;
            else if (view.Contains("2D", StringComparison.OrdinalIgnoreCase))
                tab.view = ESceneEditView.Mode_2D;
            else if (view.Contains("3D", StringComparison.OrdinalIgnoreCase))
                tab.view = ESceneEditView.Mode_3D;
        }

        Camera3D c3 = tab.viewport3d.camera;
        if (TryVec3(s, "cam3d_position", out Vector3 pos)) c3.Position = pos;
        if (TryVec3(s, "cam3d_target", out Vector3 tgt)) c3.Target = tgt;
        if (TryVec3(s, "cam3d_up", out Vector3 up)) c3.Up = up;
        float fov = s.get_Float("cam3d_fov", c3.FovY);
        if (fov > 1f) c3.FovY = fov;
        string proj = s.get_String("cam3d_proj");
        if (!string.IsNullOrEmpty(proj) && Enum.TryParse(proj, out CameraProjection p))
            c3.Projection = p;
        tab.viewport3d.camera = c3;
        float dist = s.get_Float("look_dist", tab.viewport3d.look_dist);
        if (dist > 0.1f) tab.viewport3d.look_dist = dist;

        Camera2D c2 = tab.viewport2d.camera;
        if (TryVec2(s, "cam2d_target", out Vector2 t2)) c2.Target = t2;
        float zoom = s.get_Float("cam2d_zoom", c2.Zoom);
        if (zoom > 0.01f) c2.Zoom = zoom;
        c2.Rotation = s.get_Float("cam2d_rot", c2.Rotation);
        tab.viewport2d.camera = c2;
    }

    static void Walk(object obj, string prefix, TTable tbl, bool write)
    {
        Type type = obj.GetType();
        foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            Visit(f, f.FieldType, () => f.GetValue(obj), v => f.SetValue(obj, v), prefix, tbl, write);
        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            Visit(p, p.PropertyType, () => p.GetValue(obj),
                p.CanWrite ? v => p.SetValue(obj, v) : null, prefix, tbl, write);
        }
    }

    static void Visit(MemberInfo member, Type mt, Func<object?> get, Action<object?>? set,
        string prefix, TTable tbl, bool write)
    {
        string key = prefix + member.Name;
        if (member.GetCustomAttribute<EdConfigAttribute>() != null)
        {
            if (write)
            {
                object? val = get();
                if (val != null) tbl.Set(key, Box(val));
            }
            else if (set != null && TryPath(tbl, key, out object? raw) && raw != null)
            {
                object? next = Unbox(mt, raw);
                if (next != null || !mt.IsValueType) set(next);
            }
        }

        if (!typeof(EdUi).IsAssignableFrom(mt) || mt.IsValueType) return;
        object? child = get();
        if (child == null) return;
        Walk(child, key + ".", tbl, write);
    }

    static object Box(object val)
    {
        if (val is bool or int or float or double or string) return val;
        if (val is Enum) return val.ToString()!;
        return val.ToString() ?? "";
    }

    static object? Unbox(Type t, object raw)
    {
        if (t == typeof(bool)) return raw is bool b ? b : Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        if (t == typeof(int)) return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        if (t == typeof(float)) return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
        if (t == typeof(double)) return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        if (t == typeof(string)) return raw.ToString() ?? "";
        if (t.IsEnum)
        {
            try { return Enum.Parse(t, raw.ToString() ?? "", true); }
            catch { return null; }
        }
        return raw;
    }

    static List<object> Vec(Vector2 v) => new() { v.X, v.Y };
    static List<object> Vec(Vector3 v) => new() { v.X, v.Y, v.Z };

    static bool TryVec3(TTable t, string key, out Vector3 v)
    {
        v = default;
        List<object> list = t.get_List(key);
        if (list.Count < 3) return false;
        v = new Vector3(F(list[0]), F(list[1]), F(list[2]));
        return true;
    }

    static bool TryVec2(TTable t, string key, out Vector2 v)
    {
        v = default;
        List<object> list = t.get_List(key);
        if (list.Count < 2) return false;
        v = new Vector2(F(list[0]), F(list[1]));
        return true;
    }

    static bool TryPath(TTable tbl, string path, out object? value)
    {
        value = null;
        if (tbl == null || string.IsNullOrEmpty(path)) return false;
        if (!path.Contains('.'))
            return tbl.data.TryGetValue(path, out value);
        string[] parts = path.Split('.');
        TTable t = tbl;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!t.data.TryGetValue(parts[i], out object? next) || next is not TTable n)
                return false;
            t = n;
        }
        return t.data.TryGetValue(parts[^1], out value);
    }

    static float F(object? v)
    {
        if (v is float f) return f;
        if (v is double d) return (float)d;
        if (v is int i) return i;
        if (v is long l) return l;
        return Convert.ToSingle(v, CultureInfo.InvariantCulture);
    }
}
