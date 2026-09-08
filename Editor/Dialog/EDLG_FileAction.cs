using System.Numerics;
using Editor.Scenes;
using Engine;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor.Dialog;

public enum EFileActionType
{
    Open,
    Save,
}

public class EDLG_FileAction : EdDialog
{
    public EFileActionType type;
    public string root_path = "";
    public bool as_folder;
    public List<string> extensions = new();
    public string filename = "";

    const string PopupId = "File Action##ed_file_action";

    static EDLG_FileAction? _cur;
    static Action<string?>? _on_done;
    static bool _want_open;
    static bool _open;
    static string _dir = "";
    static string _path_bar = "";
    static string _selected = "";
    static bool _ask_overwrite;
    static string _overwrite_path = "";

    public static void Run(EDLG_FileAction cfg, Action<string?> on_done, string? start_dir = null)
    {
        _cur = cfg ?? new EDLG_FileAction();
        _on_done = on_done;
        _ask_overwrite = false;
        _overwrite_path = "";
        _selected = "";

        string root = Abs(_cur.root_path);
        if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
        {
            try { Directory.CreateDirectory(root); }
            catch { }
        }
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            root = GFile.GetDir_Content(EContentDir.Game);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                root = GFile.GetDir_Content(EContentDir.Engine);
            if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
            {
                try { Directory.CreateDirectory(root); }
                catch { }
            }
        }
        _cur.root_path = root;

        string dir = Abs(start_dir ?? "");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir) || !Under(dir, root))
            dir = root;
        SetDir(dir, false);

        if (!string.IsNullOrEmpty(_cur.filename))
        {
            string name = _cur.filename;
            string maybe = Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
            string abs = Abs(maybe);
            if (File.Exists(abs) || Directory.Exists(abs))
            {
                string? parent = Path.GetDirectoryName(abs);
                if (!string.IsNullOrEmpty(parent) && Under(parent, root))
                    SetDir(parent, false);
                _cur.filename = Path.GetFileName(abs);
                _selected = abs;
            }
            else
                _cur.filename = Path.GetFileName(name);
        }

        _want_open = true;
        _open = true;
    }

    public static void SaveAsset(ImpAsset asset, Action<string>? on_done = null)
    {
        if (asset == null) return;

        string ext = asset.GetFileExtension();
        string game = GFile.GetDir_Content(EContentDir.Game);
        string engine = GFile.GetDir_Content(EContentDir.Engine);
        string root = Directory.Exists(game) ? game : engine;
        string dir = root;
        string name = asset.GetType().Name + ext;

        if (!string.IsNullOrEmpty(asset.filepath))
        {
            string abs = Abs(asset.filepath);
            string file_name = Path.GetFileName(abs);
            if (!string.IsNullOrEmpty(file_name)) name = file_name;
            string? parent = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                dir = parent;
            if (Under(abs, engine) && !Under(abs, game))
                root = engine;
        }
        else if (App.scene_current?.root is SNC_Editor_Root ed
                 && Directory.Exists(ed.file_browser.root_path)
                 && Under(ed.file_browser.root_path, root))
        {
            dir = ed.file_browser.root_path;
        }

        Run(new EDLG_FileAction
        {
            type = EFileActionType.Save,
            root_path = root,
            filename = name,
            extensions = new List<string> { ext },
        }, path =>
        {
            if (string.IsNullOrEmpty(path)) return;
            WriteAsset(asset, path);
            on_done?.Invoke(path);
        }, dir);
    }

    public static void WriteAsset(ImpAsset asset, string path)
    {
        if (asset == null || string.IsNullOrWhiteSpace(path)) return;
        string local = GFile.Make_Path_Local(path);
        string abs = GFile.Make_Path_Absolute(local);
        string? dir = Path.GetDirectoryName(abs);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        asset.filepath = local;
        asset.is_inlined = false;
        App.assets[new TFile(local)] = asset;
        asset.Save(true);
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup(PopupId);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(760, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal(PopupId, ref _open, ImGuiWindowFlags.None))
        {
            if (!_open && _on_done != null)
                Finish(null);
            return;
        }

        EDLG_FileAction cur = _cur ?? new EDLG_FileAction();
        bool save = cur.type == EFileActionType.Save;
        ImGui.TextUnformatted(save ? (cur.as_folder ? "Save Folder" : "Save") : (cur.as_folder ? "Open Folder" : "Open"));
        ImGui.SameLine();
        ImGui.TextDisabled(RootLabel(cur.root_path));

        ImGui.SetNextItemWidth(-1);
        string bar = _path_bar;
        if (ImGui.InputText("##fa_path", ref bar, 1024, ImGuiInputTextFlags.EnterReturnsTrue))
            ApplyPathBar(bar, cur);
        else
            _path_bar = bar;

        float split = 240f;
        float gap = ImGui.GetStyle().ItemSpacing.X;
        float row_h = ImGui.GetFrameHeightWithSpacing();
        float extra = cur.as_folder ? row_h : row_h * 2f;
        if (_ask_overwrite) extra += ImGui.GetTextLineHeightWithSpacing() + row_h;
        float body_h = MathF.Max(120f, ImGui.GetContentRegionAvail().Y - extra - 8f);

        ImGui.BeginChild("##fa_tree", new Vector2(split, body_h), true);
        if (Directory.Exists(cur.root_path))
            DrawDirNode(cur.root_path, true);
        else
            ImGui.TextDisabled("No folder");
        ImGui.EndChild();

        ImGui.SameLine(0, gap);
        ImGui.BeginChild("##fa_list", new Vector2(0, body_h), true);
        DrawList(cur);
        ImGui.EndChild();

        if (_ask_overwrite)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.72f, 0.35f, 1f));
            ImGui.TextUnformatted("File exists. Overwrite?");
            ImGui.PopStyleColor();
            if (ImGui.Button("Overwrite", new Vector2(100, 0)))
                Finish(_overwrite_path);
            ImGui.SameLine();
            if (ImGui.Button("Cancel##ow", new Vector2(100, 0)))
                _ask_overwrite = false;
        }
        else
        {
            if (!cur.as_folder)
            {
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 210);
                string name = cur.filename ?? "";
                bool enter = ImGui.InputText("##fa_name", ref name, 256, ImGuiInputTextFlags.EnterReturnsTrue);
                cur.filename = name;
                if (enter) Accept(cur);
                ImGui.SameLine();
            }
            string ok = save ? "Save" : "Open";
            if (ImGui.Button(ok, new Vector2(90, 0)))
                Accept(cur);
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(90, 0)))
                Finish(null);
        }

        ImGui.EndPopup();
    }

    static void DrawDirNode(string dir, bool is_root)
    {
        string name = is_root ? RootLabel(dir) : Path.GetFileName(dir);
        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;
        if (is_root) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (PathsEqual(dir, _dir)) flags |= ImGuiTreeNodeFlags.Selected;
        if (!is_root && Under(_dir, dir) && !PathsEqual(_dir, dir))
            ImGui.SetNextItemOpen(true, ImGuiCond.Once);

        ImGui.PushID(dir);
        bool open = ImGui.TreeNodeEx("##d", flags, name ?? "");
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            SetDir(dir, true);
        if (open)
        {
            try
            {
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < subs.Length; i++)
                {
                    if (Hidden(subs[i])) continue;
                    DrawDirNode(subs[i], false);
                }
            }
            catch { }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    static void DrawList(EDLG_FileAction cur)
    {
        if (string.IsNullOrEmpty(_dir) || !Directory.Exists(_dir))
        {
            ImGui.TextDisabled("No folder");
            return;
        }

        if (!PathsEqual(_dir, cur.root_path))
        {
            DirectoryInfo? parent = Directory.GetParent(_dir);
            if (parent != null && Under(parent.FullName, cur.root_path))
            {
                if (ImGui.Selectable("../##up", false, ImGuiSelectableFlags.AllowDoubleClick)
                    && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    SetDir(parent.FullName, true);
            }
        }

        try
        {
            string[] dirs = Directory.GetDirectories(_dir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (Hidden(dirs[i])) continue;
                string path = dirs[i];
                string label = Path.GetFileName(path) + "/";
                bool sel = PathsEqual(path, _selected);
                if (ImGui.Selectable(label + "###d" + i, sel, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _selected = path;
                    if (cur.as_folder)
                        cur.filename = Path.GetFileName(path);
                }
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    SetDir(path, true);
            }

            if (cur.as_folder) return;

            string[] files = Directory.GetFiles(_dir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                if (Hidden(files[i]) || !ExtOk(files[i], cur.extensions)) continue;
                string path = files[i];
                string label = Path.GetFileName(path);
                bool sel = PathsEqual(path, _selected);
                if (ImGui.Selectable(label + "###f" + i, sel, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _selected = path;
                    cur.filename = label;
                }
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _selected = path;
                    cur.filename = label;
                    Accept(cur);
                }
            }
        }
        catch { }
    }

    static void Accept(EDLG_FileAction cur)
    {
        _ask_overwrite = false;
        if (cur.as_folder)
        {
            string path = !string.IsNullOrEmpty(_selected) && Directory.Exists(_selected) ? _selected : _dir;
            if (!Directory.Exists(path) || !Under(path, cur.root_path)) return;
            Finish(path);
            return;
        }

        if (cur.type == EFileActionType.Save)
        {
            string name = (cur.filename ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            name = EnsureExt(name, cur.extensions);
            string full = Path.Combine(_dir, name);
            if (!Under(full, cur.root_path)) return;
            if (File.Exists(full))
            {
                _ask_overwrite = true;
                _overwrite_path = full;
                return;
            }
            Finish(full);
            return;
        }

        string pick = _selected;
        if (string.IsNullOrEmpty(pick) && !string.IsNullOrWhiteSpace(cur.filename))
            pick = Path.Combine(_dir, cur.filename);
        if (string.IsNullOrEmpty(pick)) return;
        if (Directory.Exists(pick))
        {
            SetDir(pick, true);
            return;
        }
        if (File.Exists(pick) && Under(pick, cur.root_path) && ExtOk(pick, cur.extensions))
            Finish(pick);
    }

    static void Finish(string? path)
    {
        ImGui.CloseCurrentPopup();
        _open = false;
        _ask_overwrite = false;
        Action<string?>? cb = _on_done;
        _on_done = null;
        _cur = null;
        if (!string.IsNullOrEmpty(path))
            path = GFile.Make_Path_Local(path);
        cb?.Invoke(path);
    }

    static void ApplyPathBar(string text, EDLG_FileAction cur)
    {
        _path_bar = text ?? "";
        string abs = Abs(_path_bar);
        if (string.IsNullOrEmpty(abs)) return;
        if (File.Exists(abs) && Under(abs, cur.root_path))
        {
            string? parent = Path.GetDirectoryName(abs);
            if (!string.IsNullOrEmpty(parent)) SetDir(parent, true);
            cur.filename = Path.GetFileName(abs);
            _selected = abs;
            return;
        }
        if (Directory.Exists(abs) && Under(abs, cur.root_path))
            SetDir(abs, true);
    }

    static void SetDir(string dir, bool sync_bar)
    {
        try { dir = Path.GetFullPath(dir); }
        catch { return; }
        if (!Directory.Exists(dir)) return;
        _dir = dir;
        _selected = "";
        _ask_overwrite = false;
        if (sync_bar || string.IsNullOrEmpty(_path_bar))
            _path_bar = Display(dir);
    }

    static string Display(string abs)
    {
        string local = GFile.Make_Path_Local(abs);
        return string.IsNullOrEmpty(local) ? abs : local;
    }

    static string Abs(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        string a = GFile.Make_Path_Absolute(path.Trim());
        try { return Path.GetFullPath(a); }
        catch { return a; }
    }

    static string RootLabel(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return "";
        if (PathsEqual(dir, GFile.GetDir_Content(EContentDir.Game))) return "Game";
        if (PathsEqual(dir, GFile.GetDir_Content(EContentDir.Engine))) return "Editor";
        string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? dir : name;
    }

    static bool ExtOk(string file, List<string> exts)
    {
        if (exts == null || exts.Count == 0) return true;
        string have = Path.GetExtension(file);
        for (int i = 0; i < exts.Count; i++)
        {
            string want = NormExt(exts[i]);
            if (string.IsNullOrEmpty(want) || want == ".*") return true;
            if (have.Equals(want, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    static string EnsureExt(string name, List<string> exts)
    {
        if (exts == null || exts.Count == 0) return name;
        if (!string.IsNullOrEmpty(Path.GetExtension(name))) return name;
        string want = NormExt(exts[0]);
        if (string.IsNullOrEmpty(want) || want == ".*") return name;
        return name + want;
    }

    static string NormExt(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return "";
        ext = ext.Trim();
        if (ext == "*" || ext == "*.*") return ".*";
        if (ext.StartsWith("*.")) return ext[1..];
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    static bool Hidden(string path)
    {
        string name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) || name.StartsWith('.');
    }

    static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static bool Under(string path, string root)
    {
        if (string.IsNullOrEmpty(root)) return true;
        try
        {
            string p = Path.GetFullPath(path).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string r = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return p.StartsWith(r, StringComparison.OrdinalIgnoreCase)
                   || PathsEqual(path, root);
        }
        catch { return false; }
    }
}
