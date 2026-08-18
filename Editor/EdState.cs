using Editor.Panel;
using Editor.Scenes;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Files;

namespace Editor;

/// <summary>
/// Project-local editor session. Written to {project}/Config/Editor.TOML and reloaded
/// the next time this project is opened.
/// </summary>
public static class EdState
{
    public const string FileName = "Editor.TOML";
    const double AutosaveSeconds = 2.0;

    static double _elapsed;
    static string _last_text = "";
    static bool _ready;

    public static string FilePath()
    {
        string root = A_Game.game?.GetRootDir();
        if (string.IsNullOrWhiteSpace(root))
        {
            return "";
        }
        return Path.Combine(root, "Config", FileName);
    }

    public static void Load(Scene_Editor editor)
    {
        _ready = false;
        _elapsed = 0;
        _last_text = "";
        if (editor == null)
        {
            return;
        }

        string path = FilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _ready = true;
            return;
        }

        try
        {
            File_TOML file = new() { filepath = path };
            if (!file.Parse())
            {
                _ready = true;
                return;
            }
            Apply(editor, FromDoc(file.doc));
            _last_text = file.doc.Write();
        }
        catch (Exception e)
        {
            Console.WriteLine("EdState.Load failed: " + e.Message);
        }
        _ready = true;
    }

    public static void Save(Scene_Editor editor)
    {
        if (editor == null)
        {
            return;
        }
        string path = FilePath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            TomlDoc doc = ToDoc(Capture(editor));
            string text = doc.Write();
            if (text == _last_text)
            {
                return;
            }
            File_TOML file = new() { filepath = path, doc = doc };
            if (file.WriteDoc())
            {
                _last_text = text;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("EdState.Save failed: " + e.Message);
        }
    }

    public static void Tick(Scene_Editor editor, double dt)
    {
        if (!_ready || editor == null)
        {
            return;
        }
        _elapsed += dt;
        if (_elapsed < AutosaveSeconds)
        {
            return;
        }
        _elapsed = 0;
        Save(editor);
    }

    static EdStateData Capture(Scene_Editor editor)
    {
        EdStateData data = new();
        data.main_tab = WindowName(editor.ui_main_tabs);
        editor.mtab_scene.State_Capture(data);
        editor.mtab_asset.State_Capture(data);
        editor.mtab_scene.file_browser.State_Capture(data.browser);
        editor.mtab_asset.file_browser.State_Capture(data.browser_asset);
        return data;
    }

    static void Apply(Scene_Editor editor, EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        editor.mtab_scene.State_Apply(data);
        editor.mtab_asset.State_Apply(data);
        editor.mtab_scene.file_browser.State_Apply(data.browser);
        editor.mtab_asset.file_browser.State_Apply(data.browser_asset);
        SelectWindow(editor.ui_main_tabs, data.main_tab);
    }

    static string WindowName(C2_TabBox tabs)
    {
        int page = 0;
        for (int i = 0; i < tabs.children.Count; i++)
        {
            if (tabs.children[i] == tabs.list_tabs)
            {
                continue;
            }
            if (page == tabs.selected_tab)
            {
                return tabs.children[i].name ?? "";
            }
            page++;
        }
        return "";
    }

    static void SelectWindow(C2_TabBox tabs, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        int page = 0;
        for (int i = 0; i < tabs.children.Count; i++)
        {
            if (tabs.children[i] == tabs.list_tabs)
            {
                continue;
            }
            if (string.Equals(tabs.children[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                tabs.selected_tab = page;
                return;
            }
            page++;
        }
    }

    static EdStateData FromDoc(TomlDoc doc)
    {
        EdStateData data = new();
        TomlTable root = doc.root;
        TomlTable window = root.GetTable("window");
        if (window != null)
        {
            data.main_tab = window.GetString("main_tab");
            data.inspector_tab = window.GetInt("inspector_tab", 1);
            data.outliner_tab = window.GetInt("outliner_tab");
            data.file_browser_expanded = window.GetBool("file_browser_expanded", true);
            data.file_browser_asset_expanded = window.GetBool("file_browser_asset_expanded", true);
            data.panel_width = window.GetFloat("panel_width", 300);
            data.browser_stretch = window.GetFloat("browser_stretch", 0.5f);
            data.browser_asset_stretch = window.GetFloat("browser_asset_stretch", 0.5f);
            data.scene_tabs_stretch = window.GetFloat("scene_tabs_stretch", 1f);
            data.asset_tabs_stretch = window.GetFloat("asset_tabs_stretch", 1f);
            data.outliner_stretch = window.GetFloat("outliner_stretch", 1f);
            data.inspector_stretch = window.GetFloat("inspector_stretch", 1.2f);
        }

        TomlTable tabs = root.GetTable("tabs");
        if (tabs != null)
        {
            data.active_scene = tabs.GetInt("active_scene");
            data.active_asset = tabs.GetInt("active_asset");
        }

        List<TomlTable> scenes = root.GetArray("open_scenes");
        for (int i = 0; i < scenes.Count; i++)
        {
            TomlTable t = scenes[i];
            EdStateScene s = new();
            s.path = t.GetString("path");
            s.edit_mode = t.GetString("edit_mode", "Mode_3D");
            s.gizmo_mode = t.GetString("gizmo_mode", "Translate");
            s.gizmo_space = t.GetString("gizmo_space", "Local");
            s.snap_translate = t.GetFloat("snap_translate", 0.25f);
            s.snap_translate_2d = t.GetFloat("snap_translate_2d", 8f);
            s.cam3_position = t.GetFloats("cam3_position", new[] { 6.5f, 4.5f, 8.5f });
            s.cam3_target = t.GetFloats("cam3_target", new[] { 0f, 0.5f, 0f });
            s.cam3_fovy = t.GetFloat("cam3_fovy", 50f);
            s.cam2_position = t.GetFloats("cam2_position", new[] { 960f, 540f });
            s.cam2_zoom = t.GetFloat("cam2_zoom", 0.5f);
            s.selected = t.GetStrings("selected", Array.Empty<string>());
            data.scenes.Add(s);
        }

        List<TomlTable> assets = root.GetArray("open_assets");
        for (int i = 0; i < assets.Count; i++)
        {
            string p = assets[i].GetString("path");
            if (!string.IsNullOrEmpty(p))
            {
                data.assets.Add(p);
            }
        }

        TomlTable browser = root.GetTable("file_browser");
        if (browser != null)
        {
            data.browser.FromTable(browser);
        }
        TomlTable browser_asset = root.GetTable("file_browser_asset");
        if (browser_asset != null)
        {
            data.browser_asset.FromTable(browser_asset);
        }
        return data;
    }

    static TomlDoc ToDoc(EdStateData data)
    {
        TomlDoc doc = new();
        doc.root.Set("saved_at", DateTime.UtcNow.ToString("o"));

        TomlTable window = doc.root.EnsureTable("window");
        window.Set("main_tab", data.main_tab ?? "");
        window.Set("inspector_tab", data.inspector_tab);
        window.Set("outliner_tab", data.outliner_tab);
        window.Set("file_browser_expanded", data.file_browser_expanded);
        window.Set("file_browser_asset_expanded", data.file_browser_asset_expanded);
        window.Set("panel_width", data.panel_width);
        window.Set("browser_stretch", data.browser_stretch);
        window.Set("browser_asset_stretch", data.browser_asset_stretch);
        window.Set("scene_tabs_stretch", data.scene_tabs_stretch);
        window.Set("asset_tabs_stretch", data.asset_tabs_stretch);
        window.Set("outliner_stretch", data.outliner_stretch);
        window.Set("inspector_stretch", data.inspector_stretch);

        TomlTable tabs = doc.root.EnsureTable("tabs");
        tabs.Set("active_scene", data.active_scene);
        tabs.Set("active_asset", data.active_asset);

        for (int i = 0; i < data.scenes.Count; i++)
        {
            EdStateScene s = data.scenes[i];
            TomlTable t = doc.root.AddArrayTable("open_scenes");
            t.Set("path", s.path ?? "");
            t.Set("edit_mode", s.edit_mode ?? "Mode_3D");
            t.Set("gizmo_mode", s.gizmo_mode ?? "Translate");
            t.Set("gizmo_space", s.gizmo_space ?? "Local");
            t.Set("snap_translate", s.snap_translate);
            t.Set("snap_translate_2d", s.snap_translate_2d);
            t.Set("cam3_position", s.cam3_position ?? new[] { 6.5f, 4.5f, 8.5f });
            t.Set("cam3_target", s.cam3_target ?? new[] { 0f, 0.5f, 0f });
            t.Set("cam3_fovy", s.cam3_fovy);
            t.Set("cam2_position", s.cam2_position ?? new[] { 960f, 540f });
            t.Set("cam2_zoom", s.cam2_zoom);
            t.Set("selected", s.selected ?? Array.Empty<string>());
        }

        for (int i = 0; i < data.assets.Count; i++)
        {
            TomlTable t = doc.root.AddArrayTable("open_assets");
            t.Set("path", data.assets[i] ?? "");
        }

        data.browser.ToTable(doc.root.EnsureTable("file_browser"));
        data.browser_asset.ToTable(doc.root.EnsureTable("file_browser_asset"));
        return doc;
    }

    /// <summary>
    /// C2_List fill weights stay small (0.5 / 1 / 1.2, or a 0-1 pair after a splitter drag).
    /// Older saves wrote pixel heights (e.g. 189) into stretch_ratio — reject those.
    /// </summary>
    public static bool Stretch_IsWeight(float v)
    {
        return v > 0f && v <= 8f;
    }

    public static string Path_Store(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        if (path.Contains("{engine}") || path.Contains("{game}"))
        {
            return CleanToken(path.Replace('\\', '/'));
        }

        string real;
        try
        {
            real = Path.GetFullPath(ImpFile.Path_Resolve(path));
        }
        catch
        {
            real = ImpFile.Path_Resolve(path);
        }

        // Game first so project files never get tagged as engine when both
        // trees sit under a folder named Content.
        string game = ImpFile.ContentDir_Game();
        if (PNL_FileBrowser.IsUnder(real, game))
        {
            return TokenUnder("{game}", game, real);
        }
        string engine = ImpFile.ContentDir_Engine();
        if (PNL_FileBrowser.IsUnder(real, engine))
        {
            return TokenUnder("{engine}", engine, real);
        }
        return real.Replace('\\', '/');
    }

    public static string Path_Load(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        path = CleanToken(path.Replace('\\', '/'));
        string resolved = File_JSON.Path_ResolveRef(null, path);
        try
        {
            return Path.GetFullPath(resolved);
        }
        catch
        {
            return resolved;
        }
    }

    static string TokenUnder(string token, string root, string real)
    {
        string rel;
        try
        {
            rel = Path.GetRelativePath(root, real).Replace('\\', '/');
        }
        catch
        {
            return token;
        }
        if (string.IsNullOrEmpty(rel) || rel == ".")
        {
            return token;
        }
        return token + "/" + rel;
    }

    static string CleanToken(string path)
    {
        if (path.EndsWith("/.") || path.EndsWith("\\."))
        {
            path = path.Substring(0, path.Length - 2);
        }
        while (path.EndsWith('/') || path.EndsWith('\\'))
        {
            path = path.Substring(0, path.Length - 1);
        }
        return path;
    }
}

public class EdStateData
{
    public string main_tab = "Scene";
    public int inspector_tab = 1;
    public int outliner_tab;
    public bool file_browser_expanded = true;
    public bool file_browser_asset_expanded = true;
    public float panel_width = 300;
    public float browser_stretch = 0.5f;
    public float browser_asset_stretch = 0.5f;
    public float scene_tabs_stretch = 1f;
    public float asset_tabs_stretch = 1f;
    public float outliner_stretch = 1f;
    public float inspector_stretch = 1.2f;
    public int active_scene;
    public int active_asset;
    public List<EdStateScene> scenes = new();
    public List<string> assets = new();
    public EdStateBrowser browser = new();
    public EdStateBrowser browser_asset = new();
}

public class EdStateScene
{
    public string path = "";
    public string edit_mode = "Mode_3D";
    public string gizmo_mode = "Translate";
    public string gizmo_space = "Local";
    public float snap_translate = 0.25f;
    public float snap_translate_2d = 8f;
    public float[] cam3_position;
    public float[] cam3_target;
    public float cam3_fovy = 50;
    public float[] cam2_position;
    public float cam2_zoom = 0.5f;
    public string[] selected;
}

public class EdStateBrowser
{
    public string current_dir = "";
    public int dir_tab;
    public string search = "";
    public bool show_source_files = true;
    public bool show_file_extensions;
    public bool show_engine_content = true;
    public float thumbnail_size = 80;
    public bool settings_open;
    public bool favorites_expanded;
    public float sidebar_width = 220;
    public List<string> favorites = new();
    public List<string> expanded = new();

    public void FromTable(TomlTable t)
    {
        if (t == null)
        {
            return;
        }
        current_dir = t.GetString("current_dir");
        dir_tab = t.GetInt("dir_tab");
        search = t.GetString("search");
        show_source_files = t.GetBool("show_source_files", true);
        show_file_extensions = t.GetBool("show_file_extensions");
        show_engine_content = t.GetBool("show_engine_content", true);
        thumbnail_size = t.GetFloat("thumbnail_size", 80);
        settings_open = t.GetBool("settings_open");
        favorites_expanded = t.GetBool("favorites_expanded");
        sidebar_width = t.GetFloat("sidebar_width", 220);
        favorites.Clear();
        string[] favs = t.GetStrings("favorites", Array.Empty<string>());
        for (int i = 0; i < favs.Length; i++)
        {
            favorites.Add(favs[i]);
        }
        expanded.Clear();
        string[] exp = t.GetStrings("expanded", Array.Empty<string>());
        for (int i = 0; i < exp.Length; i++)
        {
            expanded.Add(exp[i]);
        }
    }

    public void ToTable(TomlTable t)
    {
        if (t == null)
        {
            return;
        }
        t.Set("current_dir", current_dir ?? "");
        t.Set("dir_tab", dir_tab);
        t.Set("search", search ?? "");
        t.Set("show_source_files", show_source_files);
        t.Set("show_file_extensions", show_file_extensions);
        t.Set("show_engine_content", show_engine_content);
        t.Set("thumbnail_size", thumbnail_size);
        t.Set("settings_open", settings_open);
        t.Set("favorites_expanded", favorites_expanded);
        t.Set("sidebar_width", sidebar_width);
        t.Set("favorites", favorites.ToArray());
        t.Set("expanded", expanded.ToArray());
    }
}
