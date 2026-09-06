using System.Numerics;
using Editor.Dialog;
using Editor.UI;
using Engine.Assets;
using Engine.Core;
using Engine.Globals;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Panels;

public class PNL_SceneTree : EdPanel
{
    public EUI_SearchBar comp_search = new();

    public A_Scene? scene;
    public ImpComp? root_comp;
    public ImpComp? selected;

    public Action<ImpComp?>? on_select;
    public Action<ImpComp>? on_delete;
    public Action<ImpComp>? on_duplicate;
    public Action<ImpComp, Type>? on_add_child;
    public Action<ImpComp, ImpComp, int>? on_move;
    public Action<ImpComp>? on_rename;

    static readonly Dictionary<Type, Texture2D> _type_icons = new();
    static ImpComp? _drag;

    public override void OnDrawPanel()
    {
        comp_search.OnDraw();

        ImpComp? root = root_comp ?? scene?.root;
        if (root == null)
        {
            ImGui.TextDisabled("No scene");
            return;
        }

        ImGui.BeginChild("##tree", Vector2.Zero, false);
        DrawNode(root);

        if (ImGui.BeginPopupContextWindow("##tree_empty", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
        {
            bool can_add = root.Allow_Children() && !root.Editor_IsLocked();
            if (ImGui.MenuItem("Add Comp", "Shift+N", false, can_add))
                OpenAdd(root);
            ImGui.EndPopup();
        }

        ImGui.EndChild();

        bool tree_hot = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (tree_hot && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.F2) && selected != null && !IsNative(selected))
            on_rename?.Invoke(selected);
    }

    public void OpenAdd(ImpComp? parent)
    {
        if (parent == null || !parent.Allow_Children() || parent.Editor_IsLocked()) return;
        ImpComp p = parent;
        EDLG_PickClass.Run(typeof(ImpComp), t =>
        {
            if (t != null) on_add_child?.Invoke(p, t);
        });
    }

    static bool IsNative(ImpComp c) => c.is_builtin || c.is_child_of_prefab;

    void DrawNode(ImpComp c)
    {
        if (c == null) return;
        ImpComp? root = root_comp ?? scene?.root;
        if (c != root && IsNative(c)) return;
        if (!SearchHit(c)) return;

        bool has_visible = false;
        for (int i = 0; i < c.children.Count; i++)
        {
            if (!IsNative(c.children[i])) { has_visible = true; break; }
        }
        bool hide_kids = c.editor_locked || !has_visible;
        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (hide_kids) flags |= ImGuiTreeNodeFlags.Leaf;

        string label = string.IsNullOrEmpty(c.name) ? c.GetType().Name : c.name;

        ImGui.PushID(c.GetHashCode());
        ImGui.BeginGroup();
        bool open = ImGui.TreeNodeEx("##n", flags);
        ImGui.SameLine(0, 2);

        float icon_sz = ImGui.GetTextLineHeight();
        Texture2D ico = TypeIcon(c.GetType());
        if (ico.Id != 0)
        {
            ImGui.Image((IntPtr)ico.Id, new Vector2(icon_sz, icon_sz));
            if (ImGui.IsItemClicked() && !c.Editor_IsLocked())
                on_select?.Invoke(c);
            ImGui.SameLine(0, 4);
        }

        float btn = ImGui.GetFrameHeight();
        float toggles_w = btn * 3f + 8f;
        float name_w = MathF.Max(16f, ImGui.GetContentRegionAvail().X - toggles_w);

        bool dim = !c.is_visible || c.editor_locked;
        bool tint = dim || c.auto_globalize;
        if (tint)
        {
            Vector4 col = ImGui.GetStyle().Colors[dim ? (int)ImGuiCol.TextDisabled : (int)ImGuiCol.Text];
            if (c.auto_globalize) { col.X = MathF.Min(1f, col.X * 0.45f + 0.72f); col.Y *= 0.55f; col.Z *= 0.55f; }
            ImGui.PushStyleColor(ImGuiCol.Text, col);
        }
        if (ImGui.Selectable(label, c == selected, ImGuiSelectableFlags.None, new Vector2(name_w, 0)))
        {
            if (!c.Editor_IsLocked())
                on_select?.Invoke(c);
        }
        if (tint) ImGui.PopStyleColor();
        ImGui.EndGroup();

        DragDropNode(c);

        if (ImGui.BeginPopupContextItem("##ctx"))
        {
            if (!c.Editor_IsLocked())
                on_select?.Invoke(c);
            bool is_root = c == (root_comp ?? scene?.root);
            bool locked = c.Editor_IsLocked();
            if (ImGui.MenuItem("Add Child", null, false, !locked && !c.is_builtin && c.Allow_Children()))
                OpenAdd(c);
            if (ImGui.MenuItem("Duplicate", null, false, !locked && !is_root && c.parent != null && !c.is_builtin))
                on_duplicate?.Invoke(c);
            if (ImGui.MenuItem("Delete", null, false, !locked && !is_root && !c.is_builtin))
                on_delete?.Invoke(c);
            ImGui.EndPopup();
        }

        ImGui.SameLine(0, 2);
        DrawToggles(c);

        if (open && !c.editor_locked && c.children.Count > 0)
        {
            ImGui.Indent(18f);
            for (int i = 0; i < c.children.Count; i++)
                DrawNode(c.children[i]);
            ImGui.Unindent(18f);
        }
        ImGui.PopID();
    }

    void DrawToggles(ImpComp c)
    {
        bool locked = c.editor_locked;
        Vector4 active = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];

        if (locked) ImGui.BeginDisabled();
        bool vis = c.is_visible;
        if (vis) ImGui.PushStyleColor(ImGuiCol.Button, active);
        if (ImGui.SmallButton("V"))
        {
            c.is_visible = !c.is_visible;
            if (c.scene != null) c.scene.is_dity = true;
        }
        if (vis) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Visible");
        if (locked) ImGui.EndDisabled();

        ImGui.SameLine(0, 2);
        if (locked) ImGui.PushStyleColor(ImGuiCol.Button, active);
        if (ImGui.SmallButton("L"))
        {
            c.editor_locked = !c.editor_locked;
            if (c.scene != null) c.scene.is_dity = true;
            if (c.editor_locked && selected != null && (selected == c || selected.Is_ChildOf(c)))
                on_select?.Invoke(null);
        }
        if (locked) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Locked");

        ImGui.SameLine(0, 2);
        if (locked) ImGui.BeginDisabled();
        bool glob = c.auto_globalize;
        if (glob) ImGui.PushStyleColor(ImGuiCol.Button, active);
        if (ImGui.SmallButton("G"))
        {
            c.auto_globalize = !c.auto_globalize;
            if (c.scene != null) c.scene.is_dity = true;
        }
        if (glob) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Globalize");
        if (locked) ImGui.EndDisabled();
    }

    void DragDropNode(ImpComp c)
    {
        bool can_drag = c.parent != null && !c.is_builtin && !c.Editor_IsLocked();
        if (can_drag && ImGui.BeginDragDropSource())
        {
            _drag = c;
            int dummy = 1;
            unsafe { ImGui.SetDragDropPayload("IMP_COMP", (IntPtr)(&dummy), sizeof(int)); }
            ImGui.Text(string.IsNullOrEmpty(c.name) ? c.GetType().Name : c.name);
            ImGui.EndDragDropSource();
        }

        if (!ImGui.BeginDragDropTarget()) return;
        int zone = DropZone(c);
        if (_drag != null && CanDrop(_drag, c, zone, out ImpComp? np, out int idx))
        {
            ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload("IMP_COMP",
                ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
            bool hit;
            unsafe { hit = payload.NativePtr != null; }
            if (hit)
            {
                Vector2 rmin = ImGui.GetItemRectMin();
                Vector2 rmax = ImGui.GetItemRectMax();
                uint col = ImGui.GetColorU32(ImGuiCol.DragDropTarget);
                ImDrawListPtr dl = ImGui.GetWindowDrawList();
                if (zone == 1 && np == c)
                    dl.AddRect(rmin, rmax, col, 0f, 0, 2f);
                else
                {
                    float y = zone == 0 ? rmin.Y : rmax.Y;
                    dl.AddLine(new Vector2(rmin.X, y), new Vector2(rmax.X, y), col, 2f);
                }
                if (payload.IsDelivery())
                    on_move?.Invoke(_drag, np!, idx);
            }
        }
        ImGui.EndDragDropTarget();
    }

    static int DropZone(ImpComp c)
    {
        if (c.parent == null) return 1;
        Vector2 rmin = ImGui.GetItemRectMin();
        Vector2 rmax = ImGui.GetItemRectMax();
        float h = rmax.Y - rmin.Y;
        float t = h <= 0f ? 0.5f : (ImGui.GetIO().MousePos.Y - rmin.Y) / h;
        if (t < 0.25f) return 0;
        if (t > 0.75f) return 2;
        return 1;
    }

    static bool CanDrop(ImpComp src, ImpComp dst, int zone, out ImpComp? new_parent, out int idx)
    {
        new_parent = null;
        idx = 0;
        if (src == dst || dst.Is_ChildOf(src)) return false;
        if (src.parent == null || src.is_builtin || src.Editor_IsLocked()) return false;

        if (zone == 1)
        {
            if (!dst.Editor_IsLocked() && dst.Allow_Children())
            {
                new_parent = dst;
                idx = dst.children.Count;
                if (src.parent == dst) idx--;
                if (idx < 0) idx = 0;
                if (src.parent == dst && src.parent.children.IndexOf(src) == idx) return false;
                return true;
            }
            if (dst.parent == null) return false;
            zone = 2;
        }

        new_parent = dst.parent;
        if (new_parent == null || new_parent.Editor_IsLocked()) return false;
        if (src.parent != new_parent && !new_parent.Allow_Children()) return false;
        idx = new_parent.children.IndexOf(dst);
        if (idx < 0) return false;
        if (zone == 2) idx++;
        if (src.parent == new_parent)
        {
            int old = new_parent.children.IndexOf(src);
            if (old < 0) return false;
            if (old < idx) idx--;
            if (idx == old) return false;
        }
        return true;
    }

    bool SearchHit(ImpComp c)
    {
        string q = comp_search.search_text ?? "";
        if (string.IsNullOrWhiteSpace(q)) return true;
        if ((c.name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.GetType().Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            return true;
        if (c.editor_locked) return false;
        for (int i = 0; i < c.children.Count; i++)
        {
            if (IsNative(c.children[i])) continue;
            if (SearchHit(c.children[i])) return true;
        }
        return false;
    }

    static Texture2D TypeIcon(Type t)
    {
        if (_type_icons.TryGetValue(t, out Texture2D cached)) return cached;
        for (Type? cur = t; cur != null && typeof(ImpComp).IsAssignableFrom(cur); cur = cur.BaseType)
        {
            string abs = GFile.Make_Path_Absolute("{engine}Editor/Types/" + cur.Name + ".png");
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) continue;
            try { cached = Raylib.LoadTexture(abs); }
            catch { cached = default; }
            _type_icons[t] = cached;
            return cached;
        }
        _type_icons[t] = default;
        return default;
    }
}
