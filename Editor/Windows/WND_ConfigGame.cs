using System.Numerics;
using Editor.Panels;
using Editor.UI;
using Engine.Globals;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Windows;

/*
 *  Game config: categorizes and displays all `[ImpVar][Config] static` vars, written them to `Game.Toml` in the projects "Config" folder
 * left: class tree by inheritance
 * right: inspector for currently selected Class category
 */
public class WND_ConfigGame : EdWindow
{
    public PNL_Inspector inspector = new();
    [EdConfig] public float left_panel_width = 240f;

    readonly EUI_SearchBar search = new() { hint = "Search classes" };
    Type? selected;

    public WND_ConfigGame()
    {
        inspector.static_config = true;
        inspector.on_changed = GConfig.SaveGame;
    }

    public override void OnDraw()
    {
        base.OnDraw();

        List<Type> classes = GConfig.Gather();
        HashSet<Type> configurable = new(classes);
        Dictionary<Type, List<Type>> kids = new();
        List<Type> roots = BuildTree(classes, kids);
        if (selected == null || !configurable.Contains(selected))
            selected = FirstConfigurable(roots, kids, configurable);

        float avail_x = ImGui.GetContentRegionAvail().X;
        float splitter = 6f;
        left_panel_width = Math.Clamp(left_panel_width, 140f, MathF.Max(140f, avail_x - 180f));

        ImGui.BeginChild("##cfg_classes", new Vector2(left_panel_width, 0), true);
        search.OnDraw();
        ImGui.Separator();
        ImGui.BeginChild("##cfg_class_list", Vector2.Zero, false);
        string q = search.search_text ?? "";
        bool filtering = !string.IsNullOrWhiteSpace(q);
        for (int i = 0; i < roots.Count; i++)
            DrawNode(roots[i], kids, configurable, q, filtering);
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, 0);
        ImGui.InvisibleButton("##cfg_split", new Vector2(splitter, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive())
            left_panel_width += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);

        ImGui.SameLine(0, 0);
        ImGui.BeginChild("##cfg_inspector", Vector2.Zero, true);
        inspector.inspect_type = selected;
        inspector.selected_object = null;
        inspector.OnDrawPanel();
        ImGui.EndChild();
    }

    static List<Type> BuildTree(List<Type> classes, Dictionary<Type, List<Type>> kids)
    {
        HashSet<Type> all = new();
        for (int i = 0; i < classes.Count; i++)
        {
            for (Type? cur = classes[i]; cur != null && cur != typeof(object); cur = cur.BaseType)
                all.Add(cur);
        }

        List<Type> roots = new();
        foreach (Type t in all)
        {
            Type? p = t.BaseType;
            if (p == null || p == typeof(object) || !all.Contains(p))
            {
                roots.Add(t);
                continue;
            }
            if (!kids.TryGetValue(p, out List<Type>? list))
            {
                list = new List<Type>();
                kids[p] = list;
            }
            list.Add(t);
        }

        roots.Sort(CompareTitle);
        foreach (List<Type> list in kids.Values)
            list.Sort(CompareTitle);
        return roots;
    }

    static int CompareTitle(Type a, Type b) =>
        string.Compare(GConfig.Title(a), GConfig.Title(b), StringComparison.OrdinalIgnoreCase);

    static Type? FirstConfigurable(List<Type> roots, Dictionary<Type, List<Type>> kids, HashSet<Type> configurable)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            Type? hit = FirstConfigurable(roots[i], kids, configurable);
            if (hit != null) return hit;
        }
        return null;
    }

    static Type? FirstConfigurable(Type t, Dictionary<Type, List<Type>> kids, HashSet<Type> configurable)
    {
        if (configurable.Contains(t)) return t;
        if (!kids.TryGetValue(t, out List<Type>? list)) return null;
        for (int i = 0; i < list.Count; i++)
        {
            Type? hit = FirstConfigurable(list[i], kids, configurable);
            if (hit != null) return hit;
        }
        return null;
    }

    void DrawNode(Type t, Dictionary<Type, List<Type>> kids, HashSet<Type> configurable, string q, bool filtering)
    {
        if (!Visible(t, kids, q)) return;

        kids.TryGetValue(t, out List<Type>? list);
        bool has_kids = false;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!Visible(list[i], kids, q)) continue;
                has_kids = true;
                break;
            }
        }

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (!has_kids) flags |= ImGuiTreeNodeFlags.Leaf;
        if (t == selected) flags |= ImGuiTreeNodeFlags.Selected;
        if (has_kids)
            ImGui.SetNextItemOpen(true, filtering ? ImGuiCond.Always : ImGuiCond.Once);

        ImGui.PushID(t.FullName ?? t.Name);
        ImGui.BeginGroup();
        bool opened = ImGui.TreeNodeEx("##n", flags);
        ImGui.SameLine(0, 2);

        float icon_sz = ImGui.GetTextLineHeight();
        Texture2D ico = PNL_Inspector.TypeIcon(t);
        if (ico.Id != 0)
        {
            ImGui.Image((IntPtr)ico.Id, new Vector2(icon_sz, icon_sz));
            ImGui.SameLine(0, 4);
        }

        bool can_select = configurable.Contains(t);
        if (!can_select) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        float name_w = MathF.Max(16f, ImGui.GetContentRegionAvail().X);
        ImGui.Selectable(GConfig.Title(t), t == selected, ImGuiSelectableFlags.None, new Vector2(name_w, 0));
        if (!can_select) ImGui.PopStyleColor();
        ImGui.EndGroup();

        if (can_select && ImGui.IsItemClicked())
            selected = t;

        if (opened && has_kids)
        {
            ImGui.Indent(18f);
            for (int i = 0; i < list!.Count; i++)
                DrawNode(list[i], kids, configurable, q, filtering);
            ImGui.Unindent(18f);
        }
        ImGui.PopID();
    }

    static bool Visible(Type t, Dictionary<Type, List<Type>> kids, string q)
    {
        if (Matches(t, q)) return true;
        if (!kids.TryGetValue(t, out List<Type>? list)) return false;
        for (int i = 0; i < list.Count; i++)
            if (Visible(list[i], kids, q)) return true;
        return false;
    }

    static bool Matches(Type t, string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        if (t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        return GConfig.Title(t).Contains(q, StringComparison.OrdinalIgnoreCase);
    }
}
