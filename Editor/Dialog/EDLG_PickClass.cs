using System.Numerics;
using System.Reflection;
using Editor.UI;
using Engine;
using Engine.Globals;
using Engine.Interfaces;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Dialog;

public class EDLG_PickClass : EdDialog
{
    const string PopupId = "Pick Class##ed_pick_class";

    static bool _want_open;
    static Type _root = typeof(object);
    static Action<Type?>? _on_pick;
    static readonly EUI_SearchBar _search = new() { hint = "Filter" };
    static Type? _selected;
    static readonly List<Type> _types = new();
    static readonly List<Type> _favorites = new();
    static readonly Dictionary<Type, List<Type>> _kids = new();
    static readonly Dictionary<Type, Texture2D> _icons = new();

    public static void Run(Type root, Action<Type?> on_pick)
    {
        _root = root ?? typeof(object);
        _on_pick = on_pick;
        _search.search_text = "";
        _search.focus_next = true;
        _selected = null;
        _want_open = true;
        Rebuild();
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup(PopupId);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(420, 480), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.None))
            return;

        ImGui.TextDisabled("Root: " + _root.Name);
        _search.OnDraw();

        ImGui.BeginChild("##pick_class_list", new Vector2(0, -32), true);
        bool any_fav = false;
        for (int i = 0; i < _favorites.Count; i++)
        {
            if (!Matches(_favorites[i])) continue;
            any_fav = true;
            break;
        }
        if (any_fav)
        {
            ImGui.SeparatorText("Favorites");
            for (int i = 0; i < _favorites.Count; i++)
            {
                if (!Matches(_favorites[i])) continue;
                DrawRow(_favorites[i], "fav");
            }
            ImGui.SeparatorText("All");
        }
        DrawNode(_root);
        ImGui.EndChild();

        bool can_pick = _selected != null && !_selected.IsAbstract;
        if (!can_pick) ImGui.BeginDisabled();
        if (ImGui.Button("Select") && can_pick)
            Close(_selected);
        if (!can_pick) ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel") || !open)
            Close(null);

        ImGui.EndPopup();
    }

    static void DrawNode(Type t)
    {
        if (!Visible(t)) return;

        _kids.TryGetValue(t, out List<Type>? kids);
        bool has_kids = false;
        if (kids != null)
        {
            for (int i = 0; i < kids.Count; i++)
            {
                if (!Visible(kids[i])) continue;
                has_kids = true;
                break;
            }
        }

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (!has_kids) flags |= ImGuiTreeNodeFlags.Leaf;
        if (t == _selected) flags |= ImGuiTreeNodeFlags.Selected;

        bool filtering = !string.IsNullOrWhiteSpace(_search.search_text);
        if (has_kids)
            ImGui.SetNextItemOpen(true, filtering ? ImGuiCond.Always : ImGuiCond.Once);

        ImGui.PushID(t.FullName ?? t.Name);
        ImGui.BeginGroup();
        bool opened = ImGui.TreeNodeEx("##n", flags);
        ImGui.SameLine(0, 2);
        DrawLabel(t);
        ImGui.EndGroup();
        HitSelect(t);

        if (opened && has_kids)
        {
            ImGui.Indent(18f);
            for (int i = 0; i < kids!.Count; i++)
                DrawNode(kids[i]);
            ImGui.Unindent(18f);
        }
        ImGui.PopID();
    }

    static void DrawRow(Type t, string id)
    {
        ImGui.PushID(id + "/" + (t.FullName ?? t.Name));
        ImGui.BeginGroup();
        DrawLabel(t);
        ImGui.EndGroup();
        HitSelect(t);
        ImGui.PopID();
    }

    static void DrawLabel(Type t)
    {
        float icon_sz = ImGui.GetTextLineHeight();
        Texture2D ico = TypeIcon(t);
        if (ico.Id != 0)
        {
            ImGui.Image((IntPtr)ico.Id, new Vector2(icon_sz, icon_sz));
            ImGui.SameLine(0, 4);
        }

        if (t.IsAbstract) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        float name_w = MathF.Max(16f, ImGui.GetContentRegionAvail().X);
        ImGui.Selectable(t.Name, t == _selected, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(name_w, 0));
        if (t.IsAbstract) ImGui.PopStyleColor();
    }

    static void HitSelect(Type t)
    {
        if (t.IsAbstract) return;
        if (ImGui.IsItemClicked())
            _selected = t;
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            Close(t);
    }

    static bool Visible(Type t)
    {
        if (Matches(t)) return true;
        if (!_kids.TryGetValue(t, out List<Type>? kids)) return false;
        for (int i = 0; i < kids.Count; i++)
            if (Visible(kids[i])) return true;
        return false;
    }

    static bool Matches(Type t)
    {
        string q = _search.search_text ?? "";
        if (string.IsNullOrWhiteSpace(q)) return true;
        if (t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        string? title = t.GetCustomAttribute<TitleAttribute>()?.Name;
        return title != null && title.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    static void Close(Type? t)
    {
        ImGui.CloseCurrentPopup();
        Action<Type?>? cb = _on_pick;
        _on_pick = null;
        if (t != null) cb?.Invoke(t);
    }

    static void Rebuild()
    {
        _types.Clear();
        _kids.Clear();
        GType.ForEachOf(_root, (t, _) =>
        {
            if (t.GetCustomAttribute<ImpClassAttribute>() is { Hidden: true }) return;
            _types.Add(t);
        }, include_base: true);

        HashSet<Type> set = new(_types);
        for (int i = 0; i < _types.Count; i++)
        {
            Type t = _types[i];
            if (t == _root) continue;
            Type? p = t.BaseType;
            while (p != null && p != _root && !set.Contains(p))
                p = p.BaseType;
            if (p == null) continue;
            if (!_kids.TryGetValue(p, out List<Type>? list))
            {
                list = new List<Type>();
                _kids[p] = list;
            }
            list.Add(t);
        }

        foreach (List<Type> list in _kids.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        _favorites.Clear();
        if (!typeof(I_File).IsAssignableFrom(_root) || _root.IsAbstract) return;
        try
        {
            if (Activator.CreateInstance(_root) is not I_File file) return;
            Type[]? fav = file.File_GetFavoriteSubTypes();
            if (fav == null) return;
            HashSet<Type> seen = new();
            for (int i = 0; i < fav.Length; i++)
            {
                Type t = fav[i];
                if (t == null || t.IsAbstract || !set.Contains(t)) continue;
                if (t.GetCustomAttribute<ImpClassAttribute>() is { Hidden: true }) continue;
                if (!seen.Add(t)) continue;
                _favorites.Add(t);
            }
        }
        catch { }
    }

    static Texture2D TypeIcon(Type t)
    {
        if (_icons.TryGetValue(t, out Texture2D cached)) return cached;
        for (Type? cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
        {
            string abs = GFile.Make_Path_Absolute("{engine}Editor/Types/" + cur.Name + ".png");
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) continue;
            try { cached = Raylib.LoadTexture(abs); }
            catch { cached = default; }
            _icons[t] = cached;
            return cached;
        }
        _icons[t] = default;
        return default;
    }
}
