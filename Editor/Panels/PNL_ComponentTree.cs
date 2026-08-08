using System.Globalization;
using System.Numerics;
using System.Reflection;
using Editor.Dialog;
using ImGuiNET;
using ImperiumEngine;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace Editor.Panels;

// Outliner row toggle bits. Colors drive the far-right Visible / Locked buttons when active.
[Flags]
public enum EComponentTreeItemEditorFlags : byte
{
    None = 0,
    // Visible button off → entity is_visible = false (not drawn)
    [ColorHex("#FFA500")] Hidden = 1 << 0,
    // Locked: grey name, no world pick for self+children, children hidden from inspector
    [ColorHex("#FF0000")] Locked = 1 << 1,
}

// a tree view of the components of an input Component
public class PNL_ComponentTree : EditorPanel
{
    public ImpComponent? root_component; //single root mode
    public List<ImpComponent>? components; //flat list mode (e.g. level root entities)

    //tints every node blue to signal these are a live/instanced copy (Play-In-Editor) rather than
    //the edited level
    public bool tint_instance;

    //selection is a shared list (the level editor points this at the world panel's list
    //so viewport picking and the outliner stay in sync). Shift-click toggles membership.
    public List<ImpComponent> selection = new();
    public Action? on_selection_changed;

    //fired when the hierarchy is restructured (reparent/reorder), so the level can be marked dirty
    public Action? on_edited;

    //records a hierarchy change (add / delete / reparent) into the owning window's undo history
    public Action<IUndoable>? on_action;

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

    //context-menu deletion / duplicate, deferred (the menu is drawn mid-iteration of the tree)
    readonly List<ImpComponent> _pending_deletes = new();
    readonly List<ImpComponent> _pending_duplicates = new();

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

            // fill leftover height so right-click / drop work on the whole empty region
            DrawEmptySpaceFill(allow_root_drop: true);
        }
        else if (root_component != null)
        {
            DrawNode(root_component, null, -1);
            DrawEmptySpaceFill(allow_root_drop: false);
        }
        else
        {
            ImGui.TextDisabled("No components");
            DrawEmptySpaceFill(allow_root_drop: false);
        }

        if (_pending_items != null)
            ApplyPendingDrop();
        if (_pending_duplicates.Count > 0)
            ApplyPendingDuplicates();
        if (_pending_deletes.Count > 0)
            ApplyPendingDeletes();
    }

    void DrawNode(ImpComponent c, ImpComponent? parent, int index)
    {
        ImGui.PushID(c.guid.GetHashCode());

        float gap = 2f;
        float btn = ImGui.GetFrameHeight();
        float total_btns = btn * 2 + gap;

        // AllowOverlap: far-right V/L buttons share this row and must win hover over the
        // SpanAvailWidth hit-box (same idea as PNL_Inspector asset-slot buttons).
        var node_flags = ImGuiTreeNodeFlags.OpenOnArrow
                       | ImGuiTreeNodeFlags.OpenOnDoubleClick
                       | ImGuiTreeNodeFlags.SpanAvailWidth
                       | ImGuiTreeNodeFlags.AllowOverlap;

        // Locked nodes hide their children from the outliner (and thus from inspector selection).
        bool hide_kids = c.is_locked || c.Children.Count == 0;
        if (hide_kids) node_flags |= ImGuiTreeNodeFlags.Leaf;
        if (selection.Contains(c)) node_flags |= ImGuiTreeNodeFlags.Selected;
        if (!c.is_visible) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

        // locked name is tinted grey (instance blue still wins when playing)
        bool pushed_text = false;
        if (tint_instance)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.72f, 1f, 1f));
            pushed_text = true;
        }
        else if (c.is_locked)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.62f, 0.62f, 0.62f, 1f));
            pushed_text = true;
        }

        // 1.89+ overlap: mark the tree row as overlap-allowing so the V/L buttons submitted
        // after it can take hover/click (TreeNodeFlags.AllowOverlap alone is not always enough).
        ImGui.SetNextItemAllowOverlap();
        bool open = ImGui.TreeNodeEx($"{EditorIcons.LabelPad}{c.DisplayName}##tree", node_flags);
        EditorIcons.DrawOnLastItem(c.GetType(), tree: true);

        // tree-row state — capture before buttons become the last item
        bool row_clicked = ImGui.IsItemClicked();
        bool row_toggled_open = ImGui.IsItemToggledOpen();
        bool row_hovered = ImGui.IsItemHovered();
        var row_max = ImGui.GetItemRectMax();
        var row_min = ImGui.GetItemRectMin();

        if (pushed_text) ImGui.PopStyleColor();
        if (!c.is_visible) ImGui.PopStyleVar();

        // mouse over the reserved toggle strip on the right of this row?
        var mouse = ImGui.GetMousePos();
        bool over_toggle = mouse.X >= row_max.X - total_btns - 2f
                        && mouse.X <= row_max.X
                        && mouse.Y >= row_min.Y && mouse.Y <= row_max.Y;

        // selection / drag / context while the tree node is still ImGui's last item
        if (!over_toggle && row_clicked && !row_toggled_open)
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
        else if (!over_toggle && row_hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left)
                 && !ImGui.GetIO().KeyShift && _drag_items == null
                 && selection.Count > 1 && selection.Contains(c))
        {
            selection.Clear();
            selection.Add(c);
            on_selection_changed?.Invoke();
        }

        if (!over_toggle && ImGui.BeginDragDropSource())
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
            ImGui.Text(_drag_items.Count == 1 ? c.DisplayName : $"{_drag_items.Count} components");
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            HandleDropOnNode(c, parent, index);
            ImGui.EndDragDropTarget();
        }

        if (!over_toggle && ImGui.BeginPopupContextItem($"ctx##{c.guid}"))
        {
            if (ImGui.MenuItem("Add Child"))
                RequestAdd(c);
            if (ImGui.MenuItem("Duplicate"))
                _pending_duplicates.Add(c);
            ImGui.Separator();
            if (ImGui.MenuItem("Delete"))
                _pending_deletes.Add(c);
            ImGui.EndPopup();
        }

        // Far-right V / L. Positioned in screen space from the tree row rect so SpanAvailWidth
        // can't push them off-line; AllowOverlap on the tree lets these buttons win the hover.
        float row_h = row_max.Y - row_min.Y;
        var toggle_origin = new Vector2(row_max.X - total_btns - 2f, row_min.Y + (row_h - btn) * 0.5f);
        var cursor_backup = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(toggle_origin);
        DrawRowToggles(c, btn, gap);
        // keep layout cursor on the line below the tree row (not on the overlaid buttons)
        ImGui.SetCursorScreenPos(new Vector2(cursor_backup.X, MathF.Max(cursor_backup.Y, row_max.Y)));

        if (open)
        {
            // locked: children stay in the data model but are not shown in the outliner
            if (!c.is_locked)
            {
                var kids = c.Children;
                for (int i = 0; i < kids.Count; i++)
                    DrawNode(kids[i], c, i);
            }
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    // Visible + Locked square toggles at the current cursor (caller places the cursor).
    void DrawRowToggles(ImpComponent c, float btn, float gap)
    {
        DrawToggleButton("V", "Visible", c.is_visible,
            FlagColor(EComponentTreeItemEditorFlags.Hidden), inverted: true,
            new Vector2(btn, btn), () => ToggleVisible(c));
        ImGui.SameLine(0, gap);
        DrawToggleButton("L", "Locked", c.is_locked,
            FlagColor(EComponentTreeItemEditorFlags.Locked), inverted: false,
            new Vector2(btn, btn), () => ToggleLocked(c));
    }

    // Compact toggle: `on` drives the accent color. When `inverted`, the accent shows while off
    // (Visible button highlights orange when the entity is hidden).
    static void DrawToggleButton(string label, string tip, bool on, Vector4 accent, bool inverted,
        Vector2 size, Action on_click)
    {
        bool accent_on = inverted ? !on : on;
        if (accent_on)
        {
            var hover = Vector4.Min(accent * 1.15f + new Vector4(0.05f, 0.05f, 0.05f, 0f), Vector4.One);
            hover.W = 1f;
            var active = accent * 0.85f;
            active.W = 1f;
            ImGui.PushStyleColor(ImGuiCol.Button, accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        }

        // dim slightly when the "off" visual is showing without accent (Unlocked)
        if (!on && !inverted) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.55f);

        if (ImGui.Button($"{label}##{tip}", size)) on_click();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);

        if (!on && !inverted) ImGui.PopStyleVar();
        if (accent_on) ImGui.PopStyleColor(3);
    }

    static Vector4 FlagColor(EComponentTreeItemEditorFlags flag)
    {
        var name = flag.ToString();
        var field = typeof(EComponentTreeItemEditorFlags).GetField(name);
        var hex = field?.GetCustomAttribute<ColorHexAttribute>()?.HexValue;
        if (hex != null && TryParseHexColor(hex, out var col)) return col;
        return flag == EComponentTreeItemEditorFlags.Locked
            ? new Vector4(1f, 0f, 0f, 1f)
            : new Vector4(1f, 0.65f, 0f, 1f);
    }

    static bool TryParseHexColor(string hex, out Vector4 col)
    {
        col = default;
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6) return false;
        if (!byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, null, out byte r)) return false;
        if (!byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, null, out byte g)) return false;
        if (!byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, null, out byte b)) return false;
        col = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
        return true;
    }

    void ToggleVisible(ImpComponent c)
    {
        bool before = c.is_visible;
        bool after = !before;
        void Apply(bool v) { c.is_visible = v; on_edited?.Invoke(); on_selection_changed?.Invoke(); }
        Apply(after);
        on_action?.Invoke(new RelayUndoable(after ? "Show" : "Hide",
            () => Apply(before), () => Apply(after)));
    }

    void ToggleLocked(ImpComponent c)
    {
        bool before = c.is_locked;
        bool after = !before;
        void Apply(bool v)
        {
            c.is_locked = v;
            // locking: drop selected descendants so they leave the Entity inspector immediately
            // (children are also hidden from the outliner while the parent is locked)
            if (v)
                selection.RemoveAll(s => s != c && c.IsAncestorOf(s));
            on_edited?.Invoke();
            on_selection_changed?.Invoke();
        }
        Apply(after);
        on_action?.Invoke(new RelayUndoable(after ? "Lock" : "Unlock",
            () => Apply(before), () => Apply(after)));
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

    // Fills the remaining panel height under the tree. Right-click → Add Component anywhere in
    // that empty region (the old NoOpenOverItems window popup only hit tiny gaps between rows
    // because this fill button itself counted as an item). Flat-list mode also accepts drops
    // here to unparent to the end of the root list.
    unsafe void DrawEmptySpaceFill(bool allow_root_drop)
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.InvisibleButton("##tree_empty",
            new Vector2(Math.Max(avail.X, 1f), Math.Max(avail.Y, ImGui.GetFrameHeight())));

        if (allow_root_drop && ImGui.BeginDragDropTarget())
        {
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

        // context menu on the fill zone itself (not the window) so it isn't blocked by this item
        if (ImGui.BeginPopupContextItem("tree_ctx_empty"))
        {
            if (ImGui.MenuItem("Add Component"))
                RequestAdd(root_component);   // null in flat mode -> appends to the root list
            ImGui.EndPopup();
        }
    }

    void ApplyPendingDrop()
    {
        var items = _pending_items!;
        var new_parent = _pending_parent;
        int index = _pending_index;
        _pending_items = null;
        _pending_parent = null;

        // snapshot each item's placement before the move so the reparent can be reversed
        var before = items.ToDictionary(i => i, Capture);

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

        // record only the items whose placement actually changed
        var moved = items.Where(i => !Capture(i).Equals(before[i])).ToList();
        if (moved.Count > 0)
        {
            var after = moved.ToDictionary(i => i, Capture);
            on_action?.Invoke(new RelayUndoable("Move",
                () => { foreach (var i in moved) Attach(i, before[i]); on_edited?.Invoke(); },
                () => { foreach (var i in moved) Attach(i, after[i]);  on_edited?.Invoke(); }));
        }

        on_edited?.Invoke();
    }

    // --- add / delete via the context menus ---

    //opens the component picker; on confirm the new instance is parented under `parent`
    //(or appended to the flat root list when parent is null in flat mode)
    void RequestAdd(ImpComponent? parent)
    {
        DLG_SelectComponent.Show(inst => AddComponent(inst, parent));
    }

    //runs from the picker's callback — after the tree has drawn for the frame, so mutating the
    //hierarchy here is safe. Records the add so it can be undone.
    void AddComponent(ImpComponent inst, ImpComponent? parent)
    {
        if (parent == null && components == null) return;

        // give the new component a name unique among those already in the tree: its default (the
        // stripped type name), or default_N with the smallest free N. Manual renames may collide
        // later — only this auto-generated name is kept unique.
        inst.name = MakeUniqueName(inst.DefaultName());

        // Do runs construction (OnInit only — never Begin in the editor). Undo reverses
        // construction via Deinit (so e.g. C3_Light releases its R3D light) and detaches.
        void Do()
        {
            if (parent != null) inst.Parent_Set(parent);
            else components!.Add(inst);
            inst.Init();    // recursive OnInit (mirrors Deinit on undo)
            SelectOnly(inst);
            on_edited?.Invoke();
        }
        void Undo()
        {
            inst.Deinit();
            Detach(inst);
            selection.RemoveAll(s => s == inst || inst.IsAncestorOf(s));
            on_selection_changed?.Invoke();
            on_edited?.Invoke();
        }

        Do();
        on_action?.Invoke(new RelayUndoable($"Add {inst.DisplayName}", Undo, Do));
    }

    void ApplyPendingDuplicates()
    {
        foreach (var c in _pending_duplicates)
            DuplicateComponent(c);
        _pending_duplicates.Clear();
    }

    // Deep-clones `src` (full child hierarchy via TOML round-trip), inserts as the next sibling,
    // and records undo. Fresh guids on the whole clone so identity stays unique.
    void DuplicateComponent(ImpComponent src)
    {
        var clone = A_Entity.ReadComponentNode(A_Entity.WriteComponentNode(src));
        if (clone == null) return;

        AssignFreshGuids(clone);

        // unique display name: "Foo" → "Foo_1" among names already in the tree
        string base_name = string.IsNullOrEmpty(src.name) ? src.DefaultName() : src.name;
        clone.name = MakeUniqueName(base_name);

        ImpComponent? parent = src.parent;
        int insert_index;
        if (parent != null)
        {
            insert_index = parent.Child_IndexOf(src) + 1;
        }
        else if (components != null)
        {
            int i = components.IndexOf(src);
            if (i < 0) return;
            insert_index = i + 1;
        }
        else
        {
            // single-root mode: the sole root has no sibling list to join
            return;
        }

        void Do()
        {
            if (parent != null)
                clone.Parent_Set(parent, insert_index);
            else
                components!.Insert(Math.Clamp(insert_index, 0, components.Count), clone);
            clone.Init();   // construction only — never Begin in the editor
            SelectOnly(clone);
            on_edited?.Invoke();
        }
        void Undo()
        {
            clone.Deinit();
            Detach(clone);
            selection.RemoveAll(s => s == clone || clone.IsAncestorOf(s));
            on_selection_changed?.Invoke();
            on_edited?.Invoke();
        }

        Do();
        on_action?.Invoke(new RelayUndoable($"Duplicate {src.DisplayName}", Undo, Do));
    }

    static void AssignFreshGuids(ImpComponent c)
    {
        c.guid = Guid.NewGuid();
        foreach (var k in c.Children)
            AssignFreshGuids(k);
    }

    // picks a name not already displayed anywhere in this tree: base_name, else base_name_N with
    // the smallest N >= 1 that is free (e.g. Character, Character_1, Character_2).
    string MakeUniqueName(string base_name)
    {
        var taken = new HashSet<string>(EnumerateAll().Select(c => c.DisplayName), StringComparer.Ordinal);
        if (!taken.Contains(base_name)) return base_name;
        for (int i = 1; ; i++)
        {
            var candidate = $"{base_name}_{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    // every component in the tree, depth-first (flat root list or single root)
    IEnumerable<ImpComponent> EnumerateAll()
    {
        if (components != null)
            foreach (var c in components) foreach (var d in Descend(c)) yield return d;
        else if (root_component != null)
            foreach (var d in Descend(root_component)) yield return d;
    }

    static IEnumerable<ImpComponent> Descend(ImpComponent c)
    {
        yield return c;
        foreach (var k in c.Children) foreach (var d in Descend(k)) yield return d;
    }

    // Queues the whole current selection for deletion (the Delete hotkey). Only the top-most
    // selected nodes are queued — deleting them takes their subtrees, and thus any selected
    // descendants, along with them. Applied on the next draw via _pending_deletes.
    public void DeleteSelection()
    {
        foreach (var c in selection)
            if (!selection.Any(o => o != c && o.IsAncestorOf(c)) && !_pending_deletes.Contains(c))
                _pending_deletes.Add(c);
    }

    void ApplyPendingDeletes()
    {
        foreach (var c in _pending_deletes)
            DeleteComponent(c);
        _pending_deletes.Clear();
    }

    void DeleteComponent(ImpComponent c)
    {
        var where = Capture(c);
        // remember which selected items disappear, to restore selection on undo
        var dropped = selection.Where(s => s == c || c.IsAncestorOf(s)).ToList();

        void Do()
        {
            // Reverse construction over the whole subtree so components release what they own —
            // e.g. C3_Light deactivates its R3D light. Never OnEnd here (runtime only).
            c.Deinit();
            Detach(c);
            selection.RemoveAll(s => s == c || c.IsAncestorOf(s));
            on_selection_changed?.Invoke();
            on_edited?.Invoke();
        }
        void Undo()
        {
            Attach(c, where);
            c.Init();    // recursive construction of the restored subtree
            foreach (var s in dropped) if (!selection.Contains(s)) selection.Add(s);
            on_selection_changed?.Invoke();
            on_edited?.Invoke();
        }

        Do();
        on_action?.Invoke(new RelayUndoable($"Delete {c.DisplayName}", Undo, Do));
    }

    void SelectOnly(ImpComponent c)
    {
        selection.Clear();
        selection.Add(c);
        on_selection_changed?.Invoke();
    }

    // --- hierarchy placement (shared by add / delete / reparent undo) ---

    // a component's location in the hierarchy plus its local transform — enough to restore it
    readonly struct Placement : IEquatable<Placement>
    {
        public readonly ImpComponent? parent;   // null => the flat root list
        public readonly int index;
        public readonly bool has3d;
        public readonly ImperiumEngine.Structs.TTransform3D xf;

        public Placement(ImpComponent? parent, int index, bool has3d, ImperiumEngine.Structs.TTransform3D xf)
        { this.parent = parent; this.index = index; this.has3d = has3d; this.xf = xf; }

        public bool Equals(Placement o) =>
            parent == o.parent && index == o.index && has3d == o.has3d && xf.Equals(o.xf);
        public override bool Equals(object? o) => o is Placement p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(parent, index, has3d);
    }

    Placement Capture(ImpComponent c)
    {
        int idx = c.parent != null ? c.parent.Child_IndexOf(c) : components?.IndexOf(c) ?? -1;
        var c3 = c as ImpComponent3D;
        return new Placement(c.parent, idx, c3 != null, c3?.transform ?? default);
    }

    // detaches from wherever it currently lives (parent's children or the root list)
    void Detach(ImpComponent c)
    {
        if (c.parent != null) c.Parent_Set(null);
        components?.Remove(c);
    }

    // moves the component to the recorded placement (detaching from its current spot first)
    void Attach(ImpComponent c, Placement p)
    {
        Detach(c);
        if (p.parent != null)
            c.Parent_Set(p.parent, p.index);
        else if (components != null)
            components.Insert(Math.Clamp(p.index < 0 ? components.Count : p.index, 0, components.Count), c);

        if (p.has3d && c is ImpComponent3D c3) c3.transform = p.xf;
    }
}
