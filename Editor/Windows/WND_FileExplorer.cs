using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using Raylib_cs;

namespace Editor.Windows;

//UE-style content browser: folder tree on the left (Game/Editor tabs), tile grid on the right
public class WND_FileExplorer : EditorWindow
{
    enum ERoot { Game, Editor }

    ERoot _root = ERoot.Game;
    string _rel_path = "";  //current folder, relative to the active root's content dir
    string _selected = "";  //full path of the selected tile

    float _tree_width = 220f;
    float _tile_size = 96f;

    //cached listing of the current folder, refreshed on navigation or every second
    string _cache_key = "\0";
    double _cache_age;
    string[] _cache_dirs = [];
    string[] _cache_files = [];

    //shared across all explorer windows: default thumbnails + extension -> asset prototype
    static bool s_statics_loaded;
    static Texture2D s_thumb_folder;
    static Texture2D s_thumb_doc;
    static readonly Dictionary<string, I_EditorAsset> s_asset_types = new(StringComparer.OrdinalIgnoreCase);

    public override string Title => "File Explorer";

    string RootDir => _root == ERoot.Game
        ? Path.Combine(ImpAsset.s_projectDir, "Content")
        : ImpAsset.s_engineContentDir;

    string CurrentDir => Path.Combine(RootDir, _rel_path);

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        EnsureStatics();
        _cache_age += delta;

        DrawPathBar();
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        _tree_width = Math.Clamp(_tree_width, 120f, Math.Max(120f, avail.X - 200f));

        // --- folder tree (left) ---
        ImGui.BeginChild("tree_pane", new Vector2(_tree_width, 0), ImGuiChildFlags.Borders);
        DrawTreePane();
        ImGui.EndChild();

        ImGui.SameLine();
        SplitterVertical("tree_splitter", 4f, ref _tree_width);
        ImGui.SameLine();

        // --- tile grid (right) ---
        ImGui.BeginChild("tile_pane", new Vector2(0, 0));
        {
            float footer_h = ImGui.GetFrameHeightWithSpacing();
            ImGui.BeginChild("tiles", new Vector2(0, -footer_h));
            DrawTileGrid();
            ImGui.EndChild();
            DrawFooter();
        }
        ImGui.EndChild();
    }

    //loads the default thumbnails and builds the extension -> asset prototype registry
    static void EnsureStatics()
    {
        if (s_statics_loaded) return;
        s_statics_loaded = true;

        s_thumb_folder = LoadThumb("{engine}/2D/Thumbnails/T_thumb_folder.png");
        s_thumb_doc = LoadThumb("{engine}/2D/Thumbnails/T_thumb_doc.png");

        foreach (var type in typeof(ImpAsset).Assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(ImpAsset)) || type.IsAbstract) continue;
            if (type.GetConstructor(Type.EmptyTypes) == null) continue;
            var proto = (ImpAsset)Activator.CreateInstance(type)!;
            s_asset_types.TryAdd(proto.GetExtension(), proto);
        }
    }

    static Texture2D LoadThumb(string path)
    {
        var tex = Raylib.LoadTexture(ImpAsset.ResolvePath(path));
        if (tex.Id != 0) Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
        return tex;
    }

    // --- top bar: clickable breadcrumbs for the current path ---
    void DrawPathBar()
    {
        if (ImGui.Button("Content")) Navigate("");

        string[] segments = SplitPath(_rel_path);
        string walk = "";
        foreach (string seg in segments)
        {
            walk = Path.Combine(walk, seg);
            ImGui.SameLine(0, 4);
            ImGui.TextDisabled(">");
            ImGui.SameLine(0, 4);
            string target = walk; //capture before the loop advances
            if (ImGui.Button(seg)) Navigate(target);
        }
    }

    // --- left pane: Game/Editor tabs, each with a recursive folder tree ---
    void DrawTreePane()
    {
        if (!ImGui.BeginTabBar("root_tabs")) return;

        if (ImGui.BeginTabItem("Game"))
        {
            SetRoot(ERoot.Game);
            DrawTreeRoot();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Editor"))
        {
            SetRoot(ERoot.Editor);
            DrawTreeRoot();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    void SetRoot(ERoot root)
    {
        if (_root == root) return;
        _root = root;
        Navigate("");
    }

    void DrawTreeRoot()
    {
        if (!Directory.Exists(RootDir))
        {
            ImGui.TextDisabled("(content dir not found)");
            return;
        }

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth |
                    ImGuiTreeNodeFlags.DefaultOpen;
        if (_rel_path == "") flags |= ImGuiTreeNodeFlags.Selected;

        bool open = ImGui.TreeNodeEx("Content", flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Navigate("");
        if (!open) return;

        foreach (string dir in SafeGetDirs(RootDir))
            DrawTreeNode(dir, Path.GetFileName(dir));
        ImGui.TreePop();
    }

    void DrawTreeNode(string dir, string rel)
    {
        string[] subdirs = SafeGetDirs(dir);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    ImGuiTreeNodeFlags.SpanAvailWidth;
        if (subdirs.Length == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (rel == _rel_path) flags |= ImGuiTreeNodeFlags.Selected;

        bool open = ImGui.TreeNodeEx(Path.GetFileName(dir), flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Navigate(rel);
        if (!open) return;

        foreach (string sub in subdirs)
            DrawTreeNode(sub, Path.Combine(rel, Path.GetFileName(sub)));
        ImGui.TreePop();
    }

    // --- right pane: tile grid, folders first ---
    void DrawTileGrid()
    {
        RefreshCache();

        if (_cache_dirs.Length == 0 && _cache_files.Length == 0)
        {
            ImGui.TextDisabled("(empty)");
            return;
        }

        float label_h = ImGui.GetTextLineHeight() * 2f + 6f;
        float tile_h = _tile_size + label_h;
        float cell_w = _tile_size + ImGui.GetStyle().ItemSpacing.X;
        int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cell_w));

        int i = 0;
        foreach (string dir in _cache_dirs)
        {
            if (i++ % columns != 0) ImGui.SameLine();
            DrawTile(dir, tile_h, is_dir: true);
        }
        foreach (string file in _cache_files)
        {
            if (i++ % columns != 0) ImGui.SameLine();
            DrawTile(file, tile_h, is_dir: false);
        }
    }

    void DrawTile(string path, float tile_h, bool is_dir)
    {
        string name = Path.GetFileName(path);
        ImGui.PushID(path);

        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.Selectable("##tile", _selected == path,
                ImGuiSelectableFlags.AllowDoubleClick, new Vector2(_tile_size, tile_h)))
            _selected = path;

        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (is_dir) Navigate(Path.Combine(_rel_path, name));
            //TODO: double-clicking an asset should open it in the asset editor
        }

        //resolve thumbnail: folders always use the folder thumb; assets ask their
        //I_EditorAsset prototype (by extension), falling back to the doc thumb + white
        Texture2D tex = s_thumb_folder;
        Vector4 tint = new(1, 1, 1, 1);
        if (!is_dir)
        {
            s_asset_types.TryGetValue(Path.GetExtension(name), out var proto);
            var custom = proto?.GetThumbnailTexture() ?? default;
            tex = custom.Id != 0 ? custom : s_thumb_doc;
            var c = proto?.GetThumbnailColor() ?? Color.White;
            tint = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        var dl = ImGui.GetWindowDrawList();
        float pad = 8f;
        if (tex.Id != 0)
            dl.AddImage((IntPtr)tex.Id,
                pos + new Vector2(pad, pad),
                pos + new Vector2(_tile_size - pad, _tile_size - pad),
                Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));

        //name centered under the icon, with a dimmed type line for assets
        string label = Ellipsize(is_dir ? name : Path.GetFileNameWithoutExtension(name), _tile_size - 4f);
        float label_w = ImGui.CalcTextSize(label).X;
        dl.AddText(new Vector2(pos.X + (_tile_size - label_w) * 0.5f, pos.Y + _tile_size),
            ImGui.GetColorU32(ImGuiCol.Text), label);

        if (!is_dir)
        {
            string ext = Ellipsize(Path.GetExtension(name).TrimStart('.'), _tile_size - 4f);
            float ext_w = ImGui.CalcTextSize(ext).X;
            dl.AddText(new Vector2(pos.X + (_tile_size - ext_w) * 0.5f, pos.Y + _tile_size + ImGui.GetTextLineHeight()),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), ext);
        }

        ImGui.PopID();
    }

    void DrawFooter()
    {
        ImGui.TextDisabled($"{_cache_dirs.Length + _cache_files.Length} items");
        ImGui.SameLine(Math.Max(0, ImGui.GetContentRegionAvail().X - 110f));
        ImGui.SetNextItemWidth(110f);
        ImGui.SliderFloat("##tile_size", ref _tile_size, 48f, 160f, "");
    }

    void Navigate(string rel)
    {
        _rel_path = rel;
        _selected = "";
        _cache_key = "\0"; //force a listing refresh
    }

    void RefreshCache()
    {
        string key = CurrentDir;
        if (key == _cache_key && _cache_age < 1.0) return;
        _cache_key = key;
        _cache_age = 0;

        //if the folder vanished (deleted externally), fall back to the root
        if (!Directory.Exists(key) && _rel_path != "")
        {
            Navigate("");
            key = CurrentDir;
            _cache_key = key;
        }

        _cache_dirs = SafeGetDirs(key);
        _cache_files = SafeGetFiles(key);
    }

    static string[] SafeGetDirs(string dir)
    {
        try { return Directory.GetDirectories(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }

    static string[] SafeGetFiles(string dir)
    {
        try { return Directory.GetFiles(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }

    static string[] SplitPath(string rel) =>
        rel.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    static string Ellipsize(string s, float max_w)
    {
        if (ImGui.CalcTextSize(s).X <= max_w) return s;
        while (s.Length > 1 && ImGui.CalcTextSize(s + "..").X > max_w) s = s[..^1];
        return s + "..";
    }

    //draggable vertical bar — dragging resizes the tree pane
    static void SplitterVertical(string id, float thickness, ref float width)
    {
        ImGui.InvisibleButton(id, new Vector2(thickness, -1));
        if (ImGui.IsItemActive())
            width += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
    }
}
