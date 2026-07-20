using System.Numerics;
using ImGuiNET;
using ImperiumEngine;
using ImperiumEngine.Classes;

namespace Editor.Panels;

[Flags]
public enum EComponentTreeItemEditorFlags : byte
{
    [ColorHex("#FFA500")] Hidden, //if true, the entity is not rendered in the level editor
    [ColorHex("#FF0000")] Locked, //if true the entity will not be selectable in the level editor AND all its children will be hidden from the tree.
}

// a tree view of the components of an input Component
public class PNL_ComponentTree : EditorPanel
{
    public ImpComponent? root_component; //single root mode
    public List<ImpComponent>? components; //flat list mode (e.g. level root entities)

    //selection is a shared list (the level editor points this at the world panel's list
    //so viewport picking and the outliner stay in sync). Shift-click toggles membership.
    public List<ImpComponent> selection = new();
    public Action? on_selection_changed;

    //fired when the hierarchy is restructured (reparent/reorder), so the level can be marked dirty
    public Action? on_edited;

    const string DragPayloadType = "IMP_COMPONENT";

    //active drag state — static so it survives across frames; source panel is checked
    //on accept so a payload can't land in a different tree panel's hierarchy
    static List<ImpComponent>? _drag_items;
    static PNL_ComponentTree? _drag_source;

    //drop op deferred to the end of OnDraw so we never mutate lists mid-iteration.
    //_pending_parent == null targets the flat root list
    List<ImpComponent>? _pending_items;
    ImpComponent? _pending_parent;
    int _pending_index;

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        //drag ended somewhere outside a valid target — drop the stale references
        if (_drag_source == this && !ImGui.IsMouseDown(ImGuiMouseButton.Left)
                                 && !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _drag_items = null;
            _drag_source = null;
        }

        if (components != null)
        {
            for (int i = 0; i < components.Count; i++)
                DrawNode(components[i], null, i);

            DrawRootTailDropZone();
        }
        else if (root_component != null)
        {
            DrawNode(root_component, null, -1);
        }
        else
        {
            ImGui.TextDisabled("No components");
        }

        if (_pending_items != null)
            ApplyPendingDrop();
    }

    void DrawNode(ImpComponent c, ImpComponent? parent, int index)
    {
        var node_flags = ImGuiTreeNodeFlags.OpenOnArrow
                       | ImGuiTreeNodeFlags.OpenOnDoubleClick
                       | ImGuiTreeNodeFlags.SpanAvailWidth;

        if (c.Children.Count == 0) node_flags |= ImGuiTreeNodeFlags.Leaf;
        if (selection.Contains(c)) node_flags |= ImGuiTreeNodeFlags.Selected;
        if (!c.is_visible) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

        bool open = ImGui.TreeNodeEx($"{c.GetType().Name}##{c.guid}", node_flags);

        if (!c.is_visible) ImGui.PopStyleVar();

        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            if (ImGui.GetIO().KeyShift)
            {
                if (!selection.Remove(c)) selection.Add(c);
                on_selection_changed?.Invoke();
            }
            //keep an existing multi-selection intact on mouse-down so it can be dragged as a group
            else if (!selection.Contains(c))
            {
                selection.Clear();
                selection.Add(c);
                on_selection_changed?.Invoke();
            }
        }
        //plain click released without a drag: collapse a multi-selection to just this node
        else if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left)
                 && !ImGui.GetIO().KeyShift && _drag_items == null
                 && selection.Count > 1 && selection.Contains(c))
        {
            selection.Clear();
            selection.Add(c);
            on_selection_changed?.Invoke();
        }

        if (ImGui.BeginDragDropSource())
        {
            //dragging a node that isn't selected makes it the sole selection
            if (!selection.Contains(c))
            {
                selection.Clear();
                selection.Add(c);
                on_selection_changed?.Invoke();
            }
            _drag_items = new List<ImpComponent>(selection);
            _drag_source = this;
            ImGui.SetDragDropPayload(DragPayloadType, IntPtr.Zero, 0);
            ImGui.Text(_drag_items.Count == 1 ? c.GetType().Name : $"{_drag_items.Count} components");
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            HandleDropOnNode(c, parent, index);
            ImGui.EndDragDropTarget();
        }

        if (open)
        {
            var kids = c.Children;
            for (int i = 0; i < kids.Count; i++)
                DrawNode(kids[i], c, i);
            ImGui.TreePop();
        }
    }

    //row split into three drop zones: top quarter = insert before, bottom quarter =
    //insert after (as sibling), middle = reparent into the hovered node
    unsafe void HandleDropOnNode(ImpComponent c, ImpComponent? parent, int index)
    {
        var payload = ImGui.AcceptDragDropPayload(DragPayloadType,
            ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
        if (payload.NativePtr == null || _drag_source != this || _drag_items == null) return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        float t = (ImGui.GetMousePos().Y - min.Y) / Math.Max(max.Y - min.Y, 1f);

        //single-root mode: the root has no sibling list, so it can only receive children
        bool can_reorder = parent != null || components != null;
        int zone = !can_reorder ? 0 : t < 0.25f ? -1 : t > 0.75f ? 1 : 0;

        var new_parent = zone == 0 ? c : parent;
        bool valid = _drag_items.All(d =>
            d != new_parent && (new_parent == null || !d.IsAncestorOf(new_parent)));
        if (!valid) return;

        var dl = ImGui.GetWindowDrawList();
        uint col = ImGui.GetColorU32(ImGuiCol.DragDropTarget);
        if (zone == 0)
        {
            dl.AddRect(min, max, col);
        }
        else
        {
            float y = zone < 0 ? min.Y : max.Y;
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), col, 2f);
        }

        if (payload.IsDelivery())
        {
            _pending_items = _drag_items;
            _pending_parent = new_parent;
            _pending_index = zone == 0 ? -1 : zone < 0 ? index : index + 1;
            _drag_items = null;
            _drag_source = null;
        }
    }

    //empty space under the tree accepts drops to unparent to the end of the root list
    unsafe void DrawRootTailDropZone()
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.InvisibleButton("##root_drop_tail",
            new Vector2(Math.Max(avail.X, 1f), Math.Max(avail.Y, ImGui.GetFrameHeight())));

        if (!ImGui.BeginDragDropTarget()) return;
        var payload = ImGui.AcceptDragDropPayload(DragPayloadType);
        if (payload.NativePtr != null && _drag_source == this && _drag_items != null)
        {
            _pending_items = _drag_items;
            _pending_parent = null;
            _pending_index = components!.Count;
            _drag_items = null;
            _drag_source = null;
        }
        ImGui.EndDragDropTarget();
    }

    void ApplyPendingDrop()
    {
        var items = _pending_items!;
        var new_parent = _pending_parent;
        int index = _pending_index;
        _pending_items = null;
        _pending_parent = null;

        ImpComponent? prev = null; //dropped items land in order, each after the previous
        foreach (var item in items)
        {
            if (item == new_parent || (new_parent != null && item.IsAncestorOf(new_parent)))
                continue;

            //transforms are parent-relative: snapshot world TRS so the entity stays put
            Vector3 wpos = default, wscale = default;
            Quaternion wrot = default;
            var c3 = item as ImpComponent3D;
            c3?.GetWorldTRS(out wpos, out wrot, out wscale);

            if (new_parent == null)
            {
                //target is the flat root list
                if (components == null) continue;
                int old_root = components.IndexOf(item);
                if (old_root >= 0) components.RemoveAt(old_root);
                else item.Parent_Set(null);

                int idx = prev != null ? components.IndexOf(prev) + 1
                        : index < 0 ? components.Count
                        : Math.Clamp(old_root >= 0 && old_root < index ? index - 1 : index,
                                     0, components.Count);
                components.Insert(idx, item);
            }
            else
            {
                if (item.parent == null) components?.Remove(item);
                int idx = prev != null ? new_parent.Child_IndexOf(prev) + 1 : index;
                item.Parent_Set(new_parent, idx); //clamps & adjusts for same-parent moves
            }
            c3?.SetWorldTRS(wpos, wrot, wscale);
            prev = item;
        }

        on_edited?.Invoke();
    }
}
