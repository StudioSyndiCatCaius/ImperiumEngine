using System.Numerics;
using ImGuiNET;

namespace Editor;

public static class EdFileDialog
{
    static bool _want_open;
    static bool _open;
    static bool _save;
    static string _title = "";
    static string _dir = "";
    static string _file = "";
    static Action<string?>? _on_done;
    static readonly List<Entry> _entries = new();
    static int _sel = -1;

    struct Entry
    {
        public string name;
        public string path;
        public bool is_dir;
    }

    public static void OpenFile(string title, string filter, string initial_dir, string initial_file, Action<string?> on_done)
    {
        Begin(false, title, initial_dir, initial_file, on_done);
    }

    public static void SaveFile(string title, string filter, string initial_dir, string initial_file, Action<string?> on_done)
    {
        Begin(true, title, initial_dir, initial_file, on_done);
    }

    static void Begin(bool save, string title, string initial_dir, string initial_file, Action<string?> on_done)
    {
        _save = save;
        _title = string.IsNullOrEmpty(title) ? (save ? "Save" : "Open") : title;
        _file = initial_file ?? "";
        _on_done = on_done;
        _sel = -1;
        _dir = initial_dir ?? "";
        if (!Directory.Exists(_dir))
            _dir = Path.GetDirectoryName(_file) ?? "";
        if (!Directory.Exists(_dir))
            _dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Refresh();
        _want_open = true;
        _open = true;
    }

    public static void DrawPending()
    {
        string popup = _title + "##ed_file_dlg";
        if (_want_open)
        {
            ImGui.OpenPopup(popup);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(680, 460), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal(popup, ref _open, ImGuiWindowFlags.None))
        {
            if (!_open && _on_done != null)
                Finish(null);
            return;
        }
        if (ImGui.Button("Up"))
            GoUp();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        string dir = _dir ?? "";
        if (ImGui.InputText("##ed_file_dir", ref dir, 1024, ImGuiInputTextFlags.EnterReturnsTrue)
            && Directory.Exists(dir))
        {
            _dir = Path.GetFullPath(dir);
            Refresh();
        }
        ImGui.TextDisabled(_dir);

        ImGui.BeginChild("##ed_file_list", new Vector2(0, -40), true);
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            string label = (e.is_dir ? "[D] " : "     ") + e.name;
            bool hit = ImGui.Selectable(label + "###" + i, i == _sel, ImGuiSelectableFlags.AllowDoubleClick);
            if (hit)
            {
                _sel = i;
                if (!e.is_dir) _file = e.name;
            }
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _sel = i;
                if (e.is_dir) Enter(e.path);
                else
                {
                    _file = e.name;
                    Accept();
                }
            }
        }
        ImGui.EndChild();

        if (_save)
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 180);
            ImGui.InputText("##ed_file_name", ref _file, 256);
            ImGui.SameLine();
        }
        if (ImGui.Button(_save ? "Create" : "Open", new Vector2(80, 0)))
            Accept();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(80, 0)))
            Finish(null);
        ImGui.EndPopup();
    }

    static void Accept()
    {
        if (_save)
        {
            string name = _file ?? "";
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!name.EndsWith(".ImpGame", StringComparison.OrdinalIgnoreCase))
                name += ".ImpGame";
            Finish(Path.Combine(_dir, name));
            return;
        }

        if (_sel >= 0 && _sel < _entries.Count)
        {
            Entry e = _entries[_sel];
            if (e.is_dir)
            {
                Enter(e.path);
                return;
            }
            Finish(e.path);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_file))
        {
            string path = Path.IsPathRooted(_file) ? _file : Path.Combine(_dir, _file);
            if (File.Exists(path) || Directory.Exists(path))
                Finish(path);
        }
    }

    static void Finish(string? path)
    {
        ImGui.CloseCurrentPopup();
        _open = false;
        Action<string?>? cb = _on_done;
        _on_done = null;
        cb?.Invoke(path);
    }

    static void GoUp()
    {
        DirectoryInfo? parent = Directory.GetParent(_dir);
        if (parent == null) return;
        _dir = parent.FullName;
        Refresh();
    }

    static void Enter(string path)
    {
        if (!Directory.Exists(path)) return;
        _dir = path;
        _sel = -1;
        Refresh();
    }

    static void Refresh()
    {
        _entries.Clear();
        _sel = -1;
        if (string.IsNullOrEmpty(_dir) || !Directory.Exists(_dir)) return;

        try
        {
            foreach (string d in Directory.GetDirectories(_dir))
            {
                string name = Path.GetFileName(d);
                if (string.IsNullOrEmpty(name) || name.StartsWith('.')) continue;
                _entries.Add(new Entry { name = name, path = d, is_dir = true });
            }
            foreach (string f in Directory.GetFiles(_dir, "*.ImpGame"))
            {
                string name = Path.GetFileName(f);
                if (string.IsNullOrEmpty(name) || name.StartsWith('.')) continue;
                _entries.Add(new Entry { name = name, path = f, is_dir = false });
            }
        }
        catch { }

        _entries.Sort((a, b) =>
        {
            if (a.is_dir != b.is_dir) return a.is_dir ? -1 : 1;
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
