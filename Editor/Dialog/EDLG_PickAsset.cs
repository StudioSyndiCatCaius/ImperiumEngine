using System.Numerics;
using Editor.UI;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor.Dialog;

public class EDLG_PickAsset : EdDialog
{
    const string PopupId = "Pick Asset##ed_pick_asset";

    static bool _want_open;
    static Type _slot = typeof(ImpAsset);
    static Action<ImpAsset?>? _on_pick;
    static readonly EUI_SearchBar _search = new() { hint = "Filter" };
    static int _tab;
    static int _selected = -1;
    static int _selected_builtin = -1;
    static readonly List<ContentEntry> _content = new();

    struct ContentEntry
    {
        public string path;
        public string name;
        public string type_name;
        public Type type;
    }

    public static void Run(Type slot_type, Action<ImpAsset?> on_pick)
    {
        _slot = slot_type ?? typeof(ImpAsset);
        _on_pick = on_pick;
        _search.search_text = "";
        _search.focus_next = true;
        _tab = 0;
        _selected = -1;
        _selected_builtin = -1;
        _want_open = true;
        ScanContent();
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup(PopupId);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(560, 480), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.None))
            return;

        ImGui.TextDisabled("Slot: " + _slot.Name);
        _search.OnDraw();

        if (ImGui.BeginTabBar("##pick_tabs"))
        {
            if (ImGui.BeginTabItem("Content"))
            {
                _tab = 0;
                DrawContent();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Builtins"))
            {
                _tab = 1;
                DrawBuiltins();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.Separator();
        if (ImGui.Button("Cancel") || !open)
        {
            ImGui.CloseCurrentPopup();
            _on_pick = null;
        }
        
        ImGui.EndPopup();
    }

    static void DrawContent()
    {
        string filter = _search.search_text ?? "";
        bool filtering = !string.IsNullOrWhiteSpace(filter);
        List<int> game = new();
        List<int> engine = new();
        for (int i = 0; i < _content.Count; i++)
        {
            ContentEntry e = _content[i];
            if (!NameMatch(e.name, e.path, filter)) continue;
            if (e.path.StartsWith("{engine}", StringComparison.OrdinalIgnoreCase))
                engine.Add(i);
            else
                game.Add(i);
        }

        ImGui.BeginChild("##pick_content", new Vector2(0, -32), true);
        if (game.Count == 0 && engine.Count == 0)
            ImGui.TextDisabled("No matching assets");
        else
        {
            void DrawBranch(string id, List<int> indices, int depth)
            {
                Dictionary<string, List<int>> folders = new(StringComparer.OrdinalIgnoreCase);
                List<int> files = new();
                for (int n = 0; n < indices.Count; n++)
                {
                    int i = indices[n];
                    string rest = RelPath(_content[i].path, depth);
                    int sep = rest.IndexOfAny(['\\', '/']);
                    if (sep < 0)
                    {
                        files.Add(i);
                        continue;
                    }
                    string folder = rest.Substring(0, sep);
                    if (!folders.TryGetValue(folder, out List<int>? kids))
                    {
                        kids = new List<int>();
                        folders[folder] = kids;
                    }
                    kids.Add(i);
                }

                List<string> folder_names = folders.Keys.ToList();
                folder_names.Sort(StringComparer.OrdinalIgnoreCase);
                for (int f = 0; f < folder_names.Count; f++)
                {
                    string name = folder_names[f];
                    ImGui.PushID(id + "/" + name);
                    if (filtering) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                    if (ImGui.TreeNodeEx(name, ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth))
                    {
                        DrawBranch(id + "/" + name, folders[name], depth + 1);
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }

                files.Sort((a, b) => string.Compare(_content[a].name, _content[b].name, StringComparison.OrdinalIgnoreCase));
                for (int n = 0; n < files.Count; n++)
                {
                    int i = files[n];
                    ContentEntry e = _content[i];
                    ImGui.PushID(i);
                    ImGuiTreeNodeFlags flags =
                        ImGuiTreeNodeFlags.Leaf |
                        ImGuiTreeNodeFlags.SpanAvailWidth |
                        ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (_selected == i) flags |= ImGuiTreeNodeFlags.Selected;
                    ImGui.TreeNodeEx(e.name, flags);
                    if (ImGui.IsItemClicked())
                        _selected = i;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(e.path);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        PickContent(e);
                    ImGui.SameLine();
                    ImGui.TextDisabled(e.type_name);
                    ImGui.PopID();
                }
            }

            void DrawRoot(string label, List<int> indices)
            {
                if (indices.Count == 0) return;
                ImGui.PushID(label);
                ImGui.SetNextItemOpen(true, filtering ? ImGuiCond.Always : ImGuiCond.Once);
                if (ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen))
                {
                    DrawBranch(label, indices, 0);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }

            DrawRoot("Game", game);
            DrawRoot("Engine", engine);
        }
        ImGui.EndChild();

        if (ImGui.Button("Select") && _selected >= 0 && _selected < _content.Count)
            PickContent(_content[_selected]);
    }

    static string RelPath(string local, int depth)
    {
        string rel = local ?? "";
        if (rel.StartsWith("{game}", StringComparison.OrdinalIgnoreCase))
            rel = rel.Substring(6);
        else if (rel.StartsWith("{engine}", StringComparison.OrdinalIgnoreCase))
            rel = rel.Substring(8);
        rel = rel.TrimStart('\\', '/');
        for (int d = 0; d < depth; d++)
        {
            int sep = rel.IndexOfAny(['\\', '/']);
            if (sep < 0) return "";
            rel = rel.Substring(sep + 1);
        }
        return rel;
    }

    static void DrawBuiltins()
    {
        string filter = _search.search_text ?? "";
        bool filtering = !string.IsNullOrWhiteSpace(filter);
        IReadOnlyList<TBuiltin> all = GAsset.Builtin_All();

        Dictionary<Type, List<int>> by_type = new();
        HashSet<Type> types = new();
        for (int i = 0; i < all.Count; i++)
        {
            TBuiltin b = all[i];
            if (b.asset == null || !_slot.IsAssignableFrom(b.asset.GetType())) continue;
            if (!NameMatch(b.name, b.key, filter)) continue;
            Type t = b.asset.GetType();
            types.Add(t);
            if (!by_type.TryGetValue(t, out List<int>? list))
            {
                list = new List<int>();
                by_type[t] = list;
            }
            list.Add(i);
        }

        Dictionary<Type, List<Type>> kids = new();
        foreach (Type t in types)
        {
            Type? p = t.BaseType;
            while (p != null && p != _slot && !types.Contains(p) && p != typeof(object))
                p = p.BaseType;
            if (p == null || p == typeof(object)) p = _slot;
            if (t == p) continue;
            if (!kids.TryGetValue(p, out List<Type>? list))
            {
                list = new List<Type>();
                kids[p] = list;
            }
            if (!list.Contains(t)) list.Add(t);
        }
        foreach (List<Type> list in kids.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        ImGui.BeginChild("##pick_builtins", new Vector2(0, -32), true);
        if (by_type.Count == 0)
            ImGui.TextDisabled("No matching builtins");
        else
            DrawBuiltinType(_slot, all, by_type, kids, filtering);
        ImGui.EndChild();

        if (ImGui.Button("Select") && _selected_builtin >= 0 && _selected_builtin < all.Count)
            Close(all[_selected_builtin].asset);
    }

    static void DrawBuiltinType(Type t, IReadOnlyList<TBuiltin> all, Dictionary<Type, List<int>> by_type,
        Dictionary<Type, List<Type>> kids, bool filtering)
    {
        if (!BuiltinVisible(t, by_type, kids)) return;

        kids.TryGetValue(t, out List<Type>? type_kids);
        by_type.TryGetValue(t, out List<int>? items);
        bool has_kids = type_kids is { Count: > 0 };
        bool has_items = items is { Count: > 0 };
        if (!has_kids && !has_items) return;

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.FramePadding;
        if (!has_kids && !has_items) flags |= ImGuiTreeNodeFlags.Leaf;

        if (has_kids || has_items)
            ImGui.SetNextItemOpen(true, filtering ? ImGuiCond.Always : ImGuiCond.Once);

        ImGui.PushID(t.FullName ?? t.Name);
        if (ImGui.TreeNodeEx(t.Name, flags))
        {
            if (type_kids != null)
            {
                for (int i = 0; i < type_kids.Count; i++)
                    DrawBuiltinType(type_kids[i], all, by_type, kids, filtering);
            }
            if (items != null)
            {
                items.Sort((a, b) => string.Compare(all[a].name, all[b].name, StringComparison.OrdinalIgnoreCase));
                for (int n = 0; n < items.Count; n++)
                {
                    int i = items[n];
                    TBuiltin b = all[i];
                    ImGui.PushID(i);
                    ImGuiTreeNodeFlags leaf =
                        ImGuiTreeNodeFlags.Leaf |
                        ImGuiTreeNodeFlags.SpanAvailWidth |
                        ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (_selected_builtin == i) leaf |= ImGuiTreeNodeFlags.Selected;
                    ImGui.TreeNodeEx(b.name, leaf);
                    if (ImGui.IsItemClicked())
                        _selected_builtin = i;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(b.asset.filepath ?? b.key);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        Close(b.asset);
                    ImGui.PopID();
                }
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    static bool BuiltinVisible(Type t, Dictionary<Type, List<int>> by_type, Dictionary<Type, List<Type>> kids)
    {
        if (by_type.TryGetValue(t, out List<int>? items) && items.Count > 0) return true;
        if (!kids.TryGetValue(t, out List<Type>? type_kids)) return false;
        for (int i = 0; i < type_kids.Count; i++)
            if (BuiltinVisible(type_kids[i], by_type, kids)) return true;
        return false;
    }

    static void PickContent(ContentEntry e)
    {
        ImpAsset? asset = GAsset.Asset_Load(e.path, e.type ?? _slot);
        Close(asset);
    }

    static void Close(ImpAsset? asset)
    {
        ImGui.CloseCurrentPopup();
        Action<ImpAsset?>? cb = _on_pick;
        _on_pick = null;
        cb?.Invoke(asset);
    }

    static bool NameMatch(string a, string b, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return a.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (b ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    static void ScanContent()
    {
        _content.Clear();
        ScanDir(GFile.GetDir_Content(EContentDir.Game));
        ScanDir(GFile.GetDir_Content(EContentDir.Engine));
        _content.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
    }

    static void ScanDir(string root)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
        string[] files;
        try { files = Directory.GetFiles(root, "*", SearchOption.AllDirectories); }
        catch { return; }

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string ext = Path.GetExtension(file);
            if (!ext.Equals(".ImpAsset", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".ImpGame", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".ImpMod", StringComparison.OrdinalIgnoreCase))
                continue;

            Type? type = PeekType(file);
            if (type == null || !_slot.IsAssignableFrom(type)) continue;

            string local = GFile.Make_Path_Local(file);
            _content.Add(new ContentEntry
            {
                path = local,
                name = Path.GetFileNameWithoutExtension(file),
                type_name = type.Name,
                type = type,
            });
        }
    }

    static Type? PeekType(string abs)
    {
        try
        {
            TTable tbl = TTable.FromTOML(File.ReadAllText(abs));
            string name = tbl.get_String("type");
            if (string.IsNullOrEmpty(name)) return null;
            return TClass<object>.Resolve(name);
        }
        catch { return null; }
    }
}
