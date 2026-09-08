using System.Globalization;
using System.Numerics;
using Editor.Panels;
using Engine;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using ImGuiNET;

namespace Editor.Dialog;

public class EDLG_CreateAsset : EdDialog
{
    const string PopupId = "Create Assets##ed_create_assets";

    class Item
    {
        public ImpAsset asset = null!;
        public bool on;
        public string stem = "";
    }

    class Group
    {
        public Type type = typeof(ImpAsset);
        public string subfolder = "";
        public List<Item> items = new();
    }

    static bool _want_open;
    static string _source = "";
    static string _source_dir = "";
    static readonly List<Group> _groups = new();
    static object? _sel;
    static readonly PNL_Inspector _inspector = new();

    public static void Run(string source_path)
    {
        _source = source_path ?? "";
        _sel = null;
        _groups.Clear();
        _want_open = true;

        string local = GFile.Make_Path_Local(_source);
        string abs = GFile.Make_Path_Absolute(local);
        _source_dir = Path.GetDirectoryName(abs) ?? "";

        ImpFile? file = GFile.Import<ImpFile>(local);
        if (file == null)
        {
            file = new ImpFile { filepath = local, file_type = InferType(_source) };
            file.Reimport();
        }
        else if (file.file_type == EFileType.DataText)
        {
            EFileType inferred = InferType(_source);
            if (inferred != EFileType.DataText)
            {
                file.file_type = inferred;
                file.Reimport();
            }
        }

        string fallback = Path.GetFileNameWithoutExtension(_source);
        if (string.IsNullOrEmpty(fallback)) fallback = "Asset";

        List<ImpAsset> created = file.GetCreatableAsset() ?? new List<ImpAsset>();
        Dictionary<Type, Group> map = new();
        for (int i = 0; i < created.Count; i++)
        {
            ImpAsset a = created[i];
            if (a == null) continue;
            a.sourcefile = local;
            Type t = a.GetType();
            if (!map.TryGetValue(t, out Group? g))
            {
                g = new Group { type = t };
                map[t] = g;
                _groups.Add(g);
            }
            string stem = a.filepath;
            if (string.IsNullOrWhiteSpace(stem)) stem = fallback;
            stem = Sanitize(stem);
            bool exists = File.Exists(DestAbs(g, stem, a));
            g.items.Add(new Item { asset = a, stem = stem, on = !exists });
        }
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup(PopupId);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(760, 520), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.None))
            return;

        ImGui.TextDisabled("Source: " + (GFile.Make_Path_Local(_source) ?? ""));
        int overwrite = 0;
        int selected = 0;
        for (int g = 0; g < _groups.Count; g++)
        for (int i = 0; i < _groups[g].items.Count; i++)
        {
            Item it = _groups[g].items[i];
            if (!it.on) continue;
            selected++;
            if (File.Exists(DestAbs(_groups[g], it.stem, it.asset))) overwrite++;
        }
        if (overwrite > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.35f, 0.32f, 1f));
            ImGui.TextUnformatted(overwrite + " selected file(s) already exist and will be overwritten.");
            ImGui.PopStyleColor();
        }

        float h = MathF.Max(80f, ImGui.GetContentRegionAvail().Y - 36f);
        float w = ImGui.GetContentRegionAvail().X;
        ImGui.BeginChild("##ca_left", new Vector2(w * 0.48f, h), true);
        if (_groups.Count == 0)
            ImGui.TextDisabled("No assets can be created from this file");
        else
        {
            for (int gi = 0; gi < _groups.Count; gi++)
                DrawGroup(_groups[gi]);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##ca_right", new Vector2(0, h), true);
        DrawRight();
        ImGui.EndChild();

        bool can = selected > 0;
        if (!can) ImGui.BeginDisabled();
        if (ImGui.Button("Create", new Vector2(80, 0)) && can)
            Create();
        if (!can) ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(80, 0)) || !open)
            Close();

        ImGui.EndPopup();
    }

    static void DrawGroup(Group g)
    {
        bool all = g.items.Count > 0;
        for (int i = 0; i < g.items.Count; i++)
            if (!g.items[i].on) { all = false; break; }

        ImGui.PushID(g.type.FullName ?? g.type.Name);
        if (ImGui.Checkbox("##all", ref all))
        {
            for (int i = 0; i < g.items.Count; i++)
                g.items[i].on = all;
        }
        ImGui.SameLine();
        bool open = ImGui.TreeNodeEx(g.type.Name,
            ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
        if (ImGui.IsItemClicked())
            _sel = g;
        if (open)
        {
            for (int i = 0; i < g.items.Count; i++)
            {
                Item it = g.items[i];
                ImGui.PushID(i);
                ImGui.Checkbox("##on", ref it.on);
                ImGui.SameLine();
                bool exists = File.Exists(DestAbs(g, it.stem, it.asset));
                if (exists) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.35f, 0.32f, 1f));
                bool hit = ImGui.Selectable(it.stem + (exists ? "  (exists)" : ""), ReferenceEquals(_sel, it));
                if (exists) ImGui.PopStyleColor();
                if (hit) _sel = it;
                ImGui.PopID();
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    static void DrawRight()
    {
        if (_sel is Group g)
        {
            ImGui.SeparatorText(g.type.Name);
            ImGui.TextUnformatted("Subfolder");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##sub", ref g.subfolder, 128);
            ImGui.TextDisabled("Relative to the source file's folder (e.g. anims)");
            return;
        }

        if (_sel is Item it)
        {
            Group? owner = OwnerOf(it);
            ImGui.SeparatorText(it.asset.GetType().Name);
            ImGui.TextUnformatted("Name");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##stem", ref it.stem, 128);
            if (owner != null)
                ImGui.TextDisabled(GFile.Make_Path_Local(DestAbs(owner, it.stem, it.asset)));
            ImGui.Separator();
            _inspector.selected_object = it.asset;
            _inspector.OnDrawPanel();
            return;
        }

        ImGui.TextDisabled("Select a class or asset");
    }

    static Group? OwnerOf(Item it)
    {
        for (int g = 0; g < _groups.Count; g++)
            if (_groups[g].items.Contains(it)) return _groups[g];
        return null;
    }

    static void Create()
    {
        for (int gi = 0; gi < _groups.Count; gi++)
        {
            Group g = _groups[gi];
            for (int i = 0; i < g.items.Count; i++)
            {
                Item it = g.items[i];
                if (!it.on) continue;
                string stem = Sanitize(it.stem);
                if (string.IsNullOrEmpty(stem)) continue;
                string abs = DestAbs(g, stem, it.asset);
                EDLG_FileAction.WriteAsset(it.asset, abs);
                it.asset.Source_Reimport();
            }
        }
        Close();
    }

    static void Close()
    {
        ImGui.CloseCurrentPopup();
        _groups.Clear();
        _sel = null;
        _inspector.selected_object = null;
    }

    static string DestAbs(Group g, string stem, ImpAsset asset)
    {
        string folder = _source_dir;
        string sub = (g.subfolder ?? "").Replace('/', Path.DirectorySeparatorChar).Trim();
        if (!string.IsNullOrEmpty(sub))
            folder = Path.Combine(folder, sub);
        return Path.Combine(folder, Sanitize(stem) + asset.GetFileExtension());
    }

    static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Asset";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "Asset" : name;
    }

    static EFileType InferType(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" or "jpg" or "jpeg" or "hdr" or "exr" or "bmp" or "tga" => EFileType.Texture,
            "wav" or "ogg" or "mp3" => EFileType.Sound,
            "glb" or "gltf" or "fbx" or "obj" => EFileType.Model,
            "ttf" or "otf" => EFileType.Font,
            _ => EFileType.DataText
        };
    }
}
