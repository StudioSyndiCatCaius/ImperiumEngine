using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

[Flags]
public enum WDrawFlags
{
    Editor, Debug,
}

public class ImpComp
{
    // #################################################################################
    // Static
    // #################################################################################

    public static Type? Type_FromName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type?[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (Type? t in types)
            {
                if (t == null || t.Name != name || t.IsAbstract) continue;
                if (!typeof(ImpComp).IsAssignableFrom(t)) continue;
                return t;
            }
        }
        return null;
    }
    
    // #################################################################################
    // Class
    // #################################################################################
    
    [Category("Component")][ImpVar] public string name; //should probably be a TLabel later?

    [Category("Component")][ImpVar] public bool is_visible=true;

    private bool is_destroying=false;
    
    public readonly List<ImpComp> children=new();
    public ImpComp parent=null;
    ImpScene _scene;
    public ImpScene scene
    {
        get => _scene;
        set
        {
            if (_scene == value) return;
            _scene = value;
            for (int i = 0; i < children.Count; i++)
                children[i].scene = value;
        }
    }
    
    public ImpPlayer input_owner=null;

    public TRef<ImpScene> packed;
    public ImpComp packed_from;

    public bool IsInstanceRoot => packed_from == this && (packed.Get() != null || !string.IsNullOrEmpty(packed.path));
    public bool IsPackedForeign => packed_from != null && packed_from != this;

    // Bare ImpComp / ImpComp2D / ImpComp3D with kids is a group pivot, not a clickable volume.
    public bool IsGroupPivot
    {
        get
        {
            Type t = GetType();
            if (t != typeof(ImpComp) && t != typeof(ImpComp2D) && t != typeof(ImpComp3D)) return false;
            return children.Count > 0;
        }
    }

    public ImpComp()
    {
        name = GetType().Name;
    }

    /// <summary>
    /// A name not already used by one of <paramref name="parent"/>'s children. If `desired` is
    /// free it comes back unchanged. Otherwise its trailing digits are stripped to get a base
    /// and the lowest free number is appended: mesh -> mesh1 -> mesh2. Numbers freed by
    /// deletion get reused, so the sequence stays tight instead of climbing forever.
    /// </summary>
    public static string Name_Unique(ImpComp parent, string desired)
    {
        if (parent == null) return desired;
        if (string.IsNullOrEmpty(desired)) desired = nameof(ImpComp);
        if (!Name_Taken(parent, desired)) return desired;

        int cut = desired.Length;
        while (cut > 0 && char.IsAsciiDigit(desired[cut - 1])) cut--;
        // An all-digit name has no base left to number, so keep it whole: "12" -> "121".
        string base_name = cut > 0 ? desired[..cut] : desired;

        // Bounded by the child count - one of the first Count+1 numbers must be free.
        for (int n = 1; ; n++)
        {
            string candidate = base_name + n;
            if (!Name_Taken(parent, candidate)) return candidate;
        }
    }

    static bool Name_Taken(ImpComp parent, string name)
    {
        for (int i = 0; i < parent.children.Count; i++)
            if (string.Equals(parent.children[i].name, name, StringComparison.Ordinal)) return true;
        return false;
    }


    public void Child_Add(ImpComp child)
    {
        if (child == null || child == this) return;
        if (child.IsPackedForeign) return;
        if (IsPackedForeign || IsInstanceRoot) return;
        if (children.Contains(child))
        {
            child.parent = this;
            child.scene = scene;
            ImpComp2D.Layout_Invalidate();
            return;
        }
        child.Detach();
        children.Add(child);
        child.parent = this;
        child.scene = scene;
        ImpComp2D.Layout_Invalidate();
    }

    public void Child_Insert(int index, ImpComp child)
    {
        if (child == null || child == this) return;
        if (child.IsPackedForeign) return;
        if (IsPackedForeign || IsInstanceRoot) return;
        if (children.Contains(child))
        {
            int cur = children.IndexOf(child);
            children.RemoveAt(cur);
            if (index > cur) index--;
        }
        else child.Detach();
        if (index < 0) index = 0;
        if (index > children.Count) index = children.Count;
        children.Insert(index, child);
        child.parent = this;
        child.scene = scene;
        ImpComp2D.Layout_Invalidate();
    }

    public void Detach()
    {
        if (parent == null) return;
        parent.children.Remove(this);
        parent = null;
        scene = null;
        ImpComp2D.Layout_Invalidate();
    }

    public void Reparent(ImpComp new_parent, int index = -1)
    {
        if (new_parent == null || new_parent == this || IsAncestorOf(new_parent)) return;
        if (IsPackedForeign) return;
        if (new_parent.IsPackedForeign || new_parent.IsInstanceRoot) return;

        TTransform3? world3 = this is ImpComp3D c3 ? c3.Transform_Get(true) : null;
        TTransform2? world2 = this is ImpComp2D c2 ? c2.Transform_Get(true) : null;

        if (index < 0) new_parent.Child_Add(this);
        else new_parent.Child_Insert(index, this);

        if (world3.HasValue && this is ImpComp3D a3) a3.Transform_Set(world3.Value, true);
        if (world2.HasValue && this is ImpComp2D a2) a2.Transform_Set(world2.Value, true);
    }

    public bool IsAncestorOf(ImpComp other)
    {
        for (ImpComp p = other; p != null; p = p.parent)
            if (p == this) return true;
        return false;
    }

    public bool IsDescendantOf(ImpComp other)
    {
        return other != null && other.IsAncestorOf(this);
    }

    /// <summary>
    /// True when this comp and every ancestor is visible. Hiding a comp does not touch the
    /// is_visible of its children, so anything asking "am I on screen" has to walk up.
    /// </summary>
    public bool IsVisibleInTree()
    {
        for (ImpComp n = this; n != null; n = n.parent)
            if (!n.is_visible) return false;
        return true;
    }

    public void Child_RemoveAt(int index)
    {
        Child_GetAt(index).Destroy();
    }
    public ImpComp Child_GetAt(int index)
    {
        return children[index];
    }
    public void Child_RemoveAll()
    {
        while (children.Count > 0)
            children[0].Destroy();
    }

    public void Destroy()
    {
        if (is_destroying) return;
        is_destroying = true;
        while (children.Count > 0)
            children[0].Destroy();
        if (parent != null)
        {
            parent.children.Remove(this);
            parent = null;
        }
        scene = null;
        is_visible = false;
        ImpComp2D.Layout_Invalidate();
    }

    static readonly Dictionary<Type, FieldInfo[]> _clone_fields = new();

    static FieldInfo[] CloneFields(Type type)
    {
        if (_clone_fields.TryGetValue(type, out FieldInfo[] hit)) return hit;
        List<FieldInfo> list = new();
        foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            if (f.Name is "parent" or "children" or "input_owner" or "is_destroying" or "_scene" or "packed_from") continue;
            // Layout cache (ImpComp2D). Copying a stamp would let the clone answer with the
            // original's rect until the next epoch bump, so leave it at 0 and recompute.
            if (f.Name.StartsWith("_e_") || f.Name.StartsWith("_c_")) continue;
            list.Add(f);
        }
        FieldInfo[] arr = list.ToArray();
        _clone_fields[type] = arr;
        return arr;
    }

    public ImpComp Clone(ImpScene skip_self = null)
    {
        if (Activator.CreateInstance(GetType()) is not ImpComp copy) return null;
        foreach (FieldInfo f in CloneFields(GetType()))
        {
            object val = f.GetValue(this);
            if (f.Name == "option_button" && val == this)
            {
                f.SetValue(copy, copy);
                continue;
            }
            f.SetValue(copy, val);
        }
        copy.parent = null;
        copy.input_owner = null;
        copy.packed_from = null;
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp kid = children[i];
            if (skip_self != null && ImpScene.IsSelfInstance(kid, skip_self)) continue;
            ImpComp ck = kid.Clone(skip_self);
            if (ck != null) copy.Child_Add(ck);
        }
        if (skip_self == null && (copy.packed.Get() != null || !string.IsNullOrEmpty(copy.packed.path)))
            ImpScene.BindPackedTree(copy, copy);
        return copy;
    }
    
    
    public void Update(double dt, bool is_runtime)
    {
        // -------- INPUT
        if (input_owner != null)
        {
            if (input_owner.input_hog == null)
            {
                Update_Input(dt,input_owner);
            }
        }
        
        // -------- PROCESS
        // Layout cache lives for the whole Update phase (ImpApp bumps the epoch once).
        // Direct field writes (C2_List size/position) are visible to later children
        // because Dimensions_Get reads those fields; a per-comp epoch wipe was
        // destroying the cache (~2 bumps x every comp) and made Fill rects jitter.
        if (is_runtime) OnUpdate(dt);
        if (ImpProfiler.enabled) ImpProfiler.comp_count++;

        int n = children.Count;
        ImpComp[] snap = ChildSnap_Rent(n);
        for (int i = 0; i < n; i++)
        {
            ImpComp c = snap[i];
            if (c.parent != this) continue;
            c.Update(dt, is_runtime);
        }
        ChildSnap_Return(snap, n);
    }
    
    public void Update_Input(double dt, ImpPlayer _player)
    {
        foreach (var keystate in _player.action_states)
        {
            TLabel action = keystate.Key;
            EInputState state = keystate.Value;
            if (state == EInputState.None) continue;
            Vector3 axis = _player.InputAction_GetAxis(action);
            switch (state)
            { 
                case EInputState.Pressed: Input_Pressed(_player, action, axis); break;
                case EInputState.Released: Input_Released(_player, action, axis); break;
                case EInputState.Down: Input_Update(_player, action, dt, axis); break;
            }
        }
        
        
    }
    
    public void Draw(double dt, WDrawFlags flags, int state)
    {
        if (is_visible)
        {
            if (state == 0)
            {
                // Diagnostic-only: attributes a comp's own draw cost (not its children's) to
                // its type, so an expensive OnDraw* can be spotted without a profiler attach.
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw3D(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
            }

            ImpComp2D self2d = this as ImpComp2D;
            // Each comp draws its own content unrotated inside its own turned matrix; children
            // do the same with their own composed rotation, so nothing gets turned twice.
            bool needs_dim = state == 1 && self2d != null
                && (self2d.clip_children || self2d.transform.rotation != 0 || self2d.Scene_IsContent());
            TDimensions2 self_dim = needs_dim ? self2d.Dimensions_Get() : default;

            if (state == 1)
            {
                bool turned = Imp2D.Rotate_Push(self_dim);
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw2D(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
                Imp2D.Rotate_Pop(turned);
            }

            bool clip = state == 1 && self2d != null && self2d.clip_children;
            if (clip) Imp2D.Clip_Push(self_dim);

            int n = children.Count;
            ImpComp[] snap = ChildSnap_Rent(n);
            for (int i = 0; i < n; i++)
            {
                ImpComp c = snap[i];
                if (c.parent != this) continue;
                c.Draw(dt, flags, state);
            }
            ChildSnap_Return(snap, n);

            if (clip) Imp2D.Clip_Pop();
            if (state == 1)
            {
                bool turned = Imp2D.Rotate_Push(self_dim);
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw2DForeground(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
                Imp2D.Rotate_Pop(turned);
            }
        }
    }
    
    // ----------------------------------------------------
    // virtuals
    // ----------------------------------------------------
    
    public virtual void OnDraw2D(double dt, WDrawFlags flags) { }
    public virtual void OnDraw2DForeground(double dt, WDrawFlags flags) { }
    public virtual void OnDraw3D(double dt, WDrawFlags flags) { }
    
    public virtual void RuntimeBegin() { }
    public virtual void RuntimeEnd() { }
    public virtual void OnUpdate(double dt) { }
    
    public virtual void OnVisibilityChange(bool _is_visible) { }
    
    // ----------------------------------------------------
    // Cursor
    // ----------------------------------------------------
    public virtual void Cursor_OnEnter(ImpPlayer player) {}
    public virtual void Cursor_OnExit(ImpPlayer player) {}
    public virtual void Cursor_OnHover(ImpPlayer player, double dt) {}
    public virtual void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt) {}
    
    // ----------------------------------------------------
    // Cursor Grab (Drag & Drop)
    // ----------------------------------------------------
    public virtual bool CursorGrab_IsEnabled(ImpPlayer player) { return false;}
    //What this comp hands over when dropped somewhere. Null means it carries nothing.
    public virtual object CursorGrab_Payload() { return null; }
    public virtual void CursorGrab_Begin(ImpPlayer player) {}
    public virtual void CursorGrab_Drop(ImpPlayer player, ImpComp target) {}
    public virtual void CursorGrab_DroppedOn(ImpPlayer player, ImpComp dropped) {}
    
    //When this is the grabbed component, and the cursor is over a target
    public virtual void CursorGrab_HoveredOnTarget(ImpPlayer player, ImpComp target, bool hovered) {}
    //When this is the target to potentially drop the grabbed component
    public virtual void CursorGrab_HoveredAsTarget(ImpPlayer player, ImpComp dropped, bool hovered) {}
    public virtual void CursorGrab_Update(ImpPlayer player, double dt) {}
    
    // ----------------------------------------------------
    // Input
    // ----------------------------------------------------
    ImpComp[] ChildSnap_Rent(int n)
    {
        if (n == 0) return null;
        ImpComp[] snap = System.Buffers.ArrayPool<ImpComp>.Shared.Rent(n);
        children.CopyTo(snap);
        return snap;
    }

    static void ChildSnap_Return(ImpComp[] snap, int n)
    {
        if (snap == null) return;
        Array.Clear(snap, 0, n);
        System.Buffers.ArrayPool<ImpComp>.Shared.Return(snap);
    }

    public virtual void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis) { }
    public virtual void Input_Released(ImpPlayer player, TLabel iaction, Vector3 axis) { }
    public virtual void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis) { }
}

// ####################################################################################################################
// 2D
// ####################################################################################################################

public class ImpComp2D : ImpComp
{
    [ImpVar] public TTransform2 transform = new();
    
    [ImpVar] public float stretch_ratio=1.0f;
    [ImpVar] public EUIViewportAlignment view_alighnment_H=EUIViewportAlignment.Start;
    [ImpVar] public EUIViewportAlignment view_alighnment_V=EUIViewportAlignment.Start;
    
    [ImpVar] public Vector2 size=new(100,100);
    [ImpVar] public Vector2 size_min=Vector2.Zero;
    [ImpVar] public Vector2 size_max=Vector2.Zero;
    [ImpVar] public bool clip_children;
    [ImpVar] public Vector2 pivot=Vector2.Zero;
    [ImpVar] public bool normalize_pivot; // if true, pivot is normalized to [0,1], instead of absolute position
    
    [ImpVar][Category("Cursor")] public ECursorFilter cursor_filter=ECursorFilter.Pass;

    static bool _scene_draw;
    static TCamera2D _scene_cam;
    static Vector2 _scene_view;
    static Vector2 _scene_canvas = new(1920, 1080);
    static ImpComp _scene_root;

    // ------------------------------------------------------------
    // Layout cache
    // ------------------------------------------------------------
    //
    // Every layout primitive below re-walks the parent chain, so they compose into
    // O(depth^3) for a single Transform_Get/Dimensions_Get - and those get queried
    // several times per comp per frame (layout, draw, hit test, gizmo). Memoizing
    // against a global epoch makes repeat queries within one epoch O(1).

    static uint _layout_epoch = 1;

    // Cached value + the epoch it was computed at. Excluded from cloning (see
    // CloneFields) so a fresh copy always recomputes rather than inheriting a
    // stamp that looks current.
    uint _e_scene, _e_size, _e_anchor, _e_xform, _e_dim;
    bool _c_scene;
    Vector2 _c_size, _c_anchor;
    TTransform2 _c_xform;
    TDimensions2 _c_dim;

    /// <summary>
    /// Drops every cached layout value. Must be called after anything moves, resizes
    /// or reparents a comp outside of that comp's own OnUpdate.
    ///
    /// The transform setters, the structural Child_*/Detach/Destroy calls, the scene
    /// layout statics and the gizmo drag all invalidate for themselves. What cannot be
    /// intercepted is a direct field write - `comp.size = x`, `comp.transform.position = y` -
    /// since those are public fields. The per-phase bumps in ImpApp.Run and the per-OnUpdate
    /// bump in ImpComp.Update are the safety net that keeps those sound: a cached value
    /// never outlives the OnUpdate body that could have invalidated it. New code that
    /// mutates layout outside an OnUpdate must call this itself.
    /// </summary>
    public static void Layout_Invalidate()
    {
        // Skip 0 on wrap: the per-instance stamps default to 0, so an epoch of 0
        // would read as a hit on a comp that has never computed anything.
        if (++_layout_epoch == 0) _layout_epoch = 1;
        if (ImpProfiler.enabled) ImpProfiler.epoch_bumps++;
    }

    /// <summary>
    /// Marks the hierarchy laid out as scene content rather than editor chrome. Scene comps
    /// anchor against the canvas, honour their pivot, and rotate. Set once per frame by the
    /// scene view so layout matches whether it is queried during update, hit test, or draw.
    /// </summary>
    public static void SceneLayout_Set(ImpComp root, Vector2 canvas)
    {
        _scene_root = root;
        if (canvas.X > 1f && canvas.Y > 1f) _scene_canvas = canvas;
        Layout_Invalidate();
    }

    public static void SceneDraw_Begin(TCamera2D cam, Vector2 view_size)
    {
        _scene_draw = true;
        _scene_cam = cam;
        _scene_view = view_size;
        Layout_Invalidate();
    }

    public static void SceneDraw_End()
    {
        _scene_draw = false;
        Layout_Invalidate();
    }

    /// <summary>True when this comp is scene content (see SceneLayout_Set).</summary>
    public bool Scene_IsContent()
    {
        if (ImpProfiler.enabled) ImpProfiler.scene_calls++;
        if (_e_scene == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.scene_hits++;
            return _c_scene;
        }
        _e_scene = _layout_epoch;

        _c_scene = false;
        if (_scene_root != null)
        {
            for (ImpComp n = this; n != null; n = n.parent)
                if (n == _scene_root) { _c_scene = true; break; }
        }
        return _c_scene;
    }

    /// <summary>Local-space size after Fill resolves against the parent rect (canvas at the root).</summary>
    public Vector2 Size_Layout()
    {
        if (ImpProfiler.enabled) ImpProfiler.size_calls++;
        if (_e_size == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.size_hits++;
            return _c_size;
        }
        _e_size = _layout_epoch;

        if (!Scene_IsContent()) return _c_size = size;
        Vector2 view = parent is ImpComp2D p ? p.Size_Layout() : _scene_canvas;
        return _c_size = new Vector2(
            view_alighnment_H == EUIViewportAlignment.Fill ? view.X : size.X,
            view_alighnment_V == EUIViewportAlignment.Fill ? view.Y : size.Y);
    }

    /// <summary>Local-space offset the view alignment adds on top of transform.position.</summary>
    public Vector2 Anchor_Offset()
    {
        if (ImpProfiler.enabled) ImpProfiler.anchor_calls++;
        if (_e_anchor == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.anchor_hits++;
            return _c_anchor;
        }
        _e_anchor = _layout_epoch;

        if (!Scene_IsContent()) return _c_anchor = Vector2.Zero;
        Vector2 view = parent is ImpComp2D p ? p.Size_Layout() : _scene_canvas;
        Vector2 own = Size_Layout();
        return _c_anchor = new Vector2(
            Anchor(view_alighnment_H, view.X, own.X), Anchor(view_alighnment_V, view.Y, own.Y));
    }

    static float Anchor(EUIViewportAlignment a, float view, float own) => a switch
    {
        EUIViewportAlignment.Center => (view - own) * 0.5f,
        EUIViewportAlignment.End => view - own,
        _ => 0f,
    };

    /// <summary>Pivot as a local-space offset from the box's top-left corner.</summary>
    public Vector2 Pivot_Local()
    {
        return normalize_pivot ? pivot * Size_Layout() : pivot;
    }
    
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (size_max != Vector2.Zero) { size=Vector2.Clamp(size, size_min, size_max); }
        else { size=Vector2.Max(size, size_min); }
    }
    

    // ------------------------------------------------------------
    // Option
    // ------------------------------------------------------------
    [ImpVar][Category("Option")] public C2_Button option_button;
    
    public Action<ImpComp2D> as_option_select;
    public Action<ImpComp2D> as_option_hover;
    public Action<ImpComp2D> as_option_unhover;
    
    // ------------------------------------------------------------
    // Dimensions
    // ------------------------------------------------------------

    public TDimensions2 Dimensions_Get()
    {
        if (ImpProfiler.enabled) ImpProfiler.dim_calls++;
        if (_e_dim == _layout_epoch && ImpProfiler.layout_cache)
        {
            if (ImpProfiler.enabled) ImpProfiler.dim_hits++;
            return _c_dim;
        }
        _e_dim = _layout_epoch;

        if (_scene_draw && Scene_IsContent())
        {
            TTransform2 world = Transform_Get(true);
            Vector2 sc = world.scale;
            float z = _scene_cam.zoom <= 1e-6f ? 1f : _scene_cam.zoom;
            Vector2 px_size = Size_Layout() * new Vector2(MathF.Abs(sc.X), MathF.Abs(sc.Y)) * z;
            Vector2 px_pivot = Pivot_Local() * new Vector2(MathF.Abs(sc.X), MathF.Abs(sc.Y)) * z;

            // transform.position is the pivot point, so the box hangs off it: a normalized
            // pivot of 1,1 puts the box up and left of the same point 0,0 puts it down-right of.
            Vector2 pivot_view = ImpGizmo.WorldToView(world.position, _scene_cam, _scene_view);
            return _c_dim = new TDimensions2
            {
                position = pivot_view - px_pivot,
                size = px_size,
                rotation = (float)world.rotation,
                pivot = px_pivot,
            };
        }

        View_Get(out Vector2 vs, out Vector2 vsz);
        Vector2 pos = transform.position;

        // Inside a C2_List / C2_ScrollBox, Fill on the main axis uses size set by
        // the list (stretch_ratio share). Cross-axis Fill and Fill outside a list
        // still expand to the parent view. The list's own scroll_box is a viewport.
        C2_List parent_list = parent as C2_List;
        C2_ScrollBox parent_scroll = parent as C2_ScrollBox;
        bool is_list_scroll = parent_list != null && parent_list.scroll_box == this;
        EUIAlignment layout_align = EUIAlignment.Vertical;
        bool in_layout = false;
        if (parent_list != null && !is_list_scroll)
        {
            in_layout = true;
            layout_align = parent_list.alignment;
        }
        else if (parent_scroll != null)
        {
            in_layout = true;
            layout_align = parent_scroll.alignment;
        }
        bool list_main_h = in_layout && layout_align == EUIAlignment.Horizontal;
        bool list_main_v = in_layout && layout_align == EUIAlignment.Vertical;

        Vector2 sz = new(
            view_alighnment_H == EUIViewportAlignment.Fill
                ? (list_main_h ? size.X : vsz.X)
                : size.X,
            view_alighnment_V == EUIViewportAlignment.Fill
                ? (list_main_v ? size.Y : vsz.Y)
                : size.Y);
        Vector2 place = new(
            Align(view_alighnment_H, vs.X, vsz.X, sz.X, pos.X),
            Align(view_alighnment_V, vs.Y, vsz.Y, sz.Y, pos.Y));
        // Same rule as scene content: the placed point is the pivot, the box hangs off it.
        Vector2 pv = normalize_pivot ? pivot * sz : pivot;
        return _c_dim = new TDimensions2
        {
            position = place - pv,
            size = sz,
            rotation = (float)transform.rotation,
            pivot = pv,
        };
    }

    void View_Get(out Vector2 start, out Vector2 view_size)
    {
        if (parent is ImpComp2D p)
        {
            var d = p.Dimensions_Get();
            start = d.position;
            view_size = d.size;
        }
        else
        {
            start = Vector2.Zero;
            view_size = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        }
    }

    static float Align(EUIViewportAlignment a, float view_start, float view_size, float self_size, float offset) => a switch
    {
        EUIViewportAlignment.Center => view_start + (view_size - self_size) * 0.5f + offset,
        EUIViewportAlignment.End => view_start + view_size - self_size - offset,
        EUIViewportAlignment.Fill => view_start + offset,
        _ => view_start + offset,
    };
    
    // ------------------------------------------------------------
    // Transform
    // ------------------------------------------------------------

    public void Transform_Set(TTransform2 value, bool global = true)
    {
        TTransform2 local = !global || parent is not ImpComp2D p
            ? value
            : LocalFromWorld(p.Transform_Get(true), value);
        local.position -= Anchor_Offset();
        transform = local;
        Layout_Invalidate();
    }

    public TTransform2 Transform_Get(bool global = true)
    {
        // Only the global result is cached. The local one is transform + Anchor_Offset(),
        // and Anchor_Offset carries its own cache, so there is nothing left to save.
        if (global)
        {
            if (ImpProfiler.enabled) ImpProfiler.xform_calls++;
            if (_e_xform == _layout_epoch && ImpProfiler.layout_cache)
            {
                if (ImpProfiler.enabled) ImpProfiler.xform_hits++;
                return _c_xform;
            }
            _e_xform = _layout_epoch;
        }

        TTransform2 local = transform;
        local.position += Anchor_Offset();
        if (!global) return local;
        if (parent is not ImpComp2D p) return _c_xform = local;
        return _c_xform = WorldFromLocal(p.Transform_Get(true), local);
    }

    public Vector2 Position_Get(bool global = true)
    {
        return Transform_Get(global).position;
    }
    public void Position_Set(Vector2 position, bool global = true)
    {
        if (!global || parent is not ImpComp2D p)
        {
            transform.position = position - Anchor_Offset();
            Layout_Invalidate();
            return;
        }
        var parent_w = p.Transform_Get(true);
        TTransform2 world = new()
        {
            position = position,
            rotation = parent_w.rotation + transform.rotation,
            scale = parent_w.scale * transform.scale,
        };
        transform.position = LocalFromWorld(parent_w, world).position - Anchor_Offset();
        Layout_Invalidate();
    }
    public float Rotation_Get(bool global = true)
    {
        return (float)Transform_Get(global).rotation;
    }
    public void Rotation_Set(float rotation, bool global = true)
    {
        if (!global || parent is not ImpComp2D p)
            transform.rotation = rotation;
        else
            transform.rotation = rotation - p.Transform_Get(true).rotation;
        Layout_Invalidate();
    }
    public Vector2 Scale_Get(bool global = true)
    {
        return Transform_Get(global).scale;
    }
    public void Scale_Set(Vector2 scale, bool global = true)
    {
        if (!global || parent is not ImpComp2D p)
        {
            transform.scale = scale;
            Layout_Invalidate();
            return;
        }
        var ps = p.Transform_Get(true).scale;
        transform.scale = new Vector2(
            ps.X != 0 ? scale.X / ps.X : 0,
            ps.Y != 0 ? scale.Y / ps.Y : 0);
        Layout_Invalidate();
    }

    static TTransform2 WorldFromLocal(TTransform2 parent, TTransform2 local)
    {
        float rad = (float)(parent.rotation * (Math.PI / 180.0));
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        Vector2 scaled = local.position * parent.scale;
        return new TTransform2
        {
            position = parent.position + new Vector2(scaled.X * c - scaled.Y * s, scaled.X * s + scaled.Y * c),
            rotation = parent.rotation + local.rotation,
            scale = parent.scale * local.scale
        };
    }

    static TTransform2 LocalFromWorld(TTransform2 parent, TTransform2 world)
    {
        Vector2 inv_s = new(
            parent.scale.X != 0 ? 1f / parent.scale.X : 0,
            parent.scale.Y != 0 ? 1f / parent.scale.Y : 0);
        float rad = (float)(-parent.rotation * (Math.PI / 180.0));
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        Vector2 d = world.position - parent.position;
        return new TTransform2
        {
            position = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c) * inv_s,
            rotation = world.rotation - parent.rotation,
            scale = world.scale * inv_s
        };
    }
}


// ####################################################################################################################
// 3D
// ####################################################################################################################

public class ImpComp3D : ImpComp
{
    [Category("Transform")][ImpVar] public TTransform3 transform = new();
    [Category("Physics")][ImpVar] public bool physics_enabled;
    public A_CollisionPreset collision_preset=A_CollisionPreset.PRESET_NONE;
    
    // ------------------------------------------------------------
    // Transform
    // ------------------------------------------------------------
    
    public void Transform_Set(TTransform3 value, bool world_space = false)
    {
        if (!world_space || parent is not ImpComp3D p)
            transform = value;
        else
            transform = LocalFromWorld(p.Transform_Get(true), value);
    }

    public TTransform3 Transform_Get(bool world_space = false)
    {
        if (!world_space || parent is not ImpComp3D p)
            return transform;
        return WorldFromLocal(p.Transform_Get(true), transform);
    }

    public Vector3 Position_Get(bool world_space = false)
    {
        return Transform_Get(world_space).position;
    }
    public void Position_Set(Vector3 position, bool world_space = false)
    {
        if (!world_space || parent is not ImpComp3D p)
        {
            transform.position = position;
            return;
        }
        var parent_w = p.Transform_Get(true);
        var q = EulerToQuat(parent_w.rotation);
        Vector3 inv_s = new(
            parent_w.scale.X != 0 ? 1f / parent_w.scale.X : 0,
            parent_w.scale.Y != 0 ? 1f / parent_w.scale.Y : 0,
            parent_w.scale.Z != 0 ? 1f / parent_w.scale.Z : 0);
        transform.position = Vector3.Transform(position - parent_w.position, Quaternion.Inverse(q)) * inv_s;
    }
    public Vector3 Rotation_Get(bool world_space = false)
    {
        return Transform_Get(world_space).rotation;
    }
    public void Rotation_Set(Vector3 rotation, bool world_space = false)
    {
        if (!world_space || parent is not ImpComp3D p)
        {
            transform.rotation = rotation;
            return;
        }
        var parent_q = EulerToQuat(p.Transform_Get(true).rotation);
        transform.rotation = QuatToEuler(Quaternion.Inverse(parent_q) * EulerToQuat(rotation));
    }
    public Vector3 Scale_Get(bool world_space = false)
    {
        return Transform_Get(world_space).scale;
    }
    public void Scale_Set(Vector3 scale, bool world_space = false)
    {
        if (!world_space || parent is not ImpComp3D p)
        {
            transform.scale = scale;
            return;
        }
        var ps = p.Transform_Get(true).scale;
        transform.scale = new Vector3(
            ps.X != 0 ? scale.X / ps.X : 0,
            ps.Y != 0 ? scale.Y / ps.Y : 0,
            ps.Z != 0 ? scale.Z / ps.Z : 0);
    }

    static TTransform3 WorldFromLocal(TTransform3 parent, TTransform3 local)
    {
        var pq = EulerToQuat(parent.rotation);
        return new TTransform3
        {
            position = parent.position + Vector3.Transform(local.position * parent.scale, pq),
            rotation = QuatToEuler(pq * EulerToQuat(local.rotation)),
            scale = parent.scale * local.scale
        };
    }

    static TTransform3 LocalFromWorld(TTransform3 parent, TTransform3 world)
    {
        var pq = EulerToQuat(parent.rotation);
        Vector3 inv_s = new(
            parent.scale.X != 0 ? 1f / parent.scale.X : 0,
            parent.scale.Y != 0 ? 1f / parent.scale.Y : 0,
            parent.scale.Z != 0 ? 1f / parent.scale.Z : 0);
        return new TTransform3
        {
            position = Vector3.Transform(world.position - parent.position, Quaternion.Inverse(pq)) * inv_s,
            rotation = QuatToEuler(Quaternion.Inverse(pq) * EulerToQuat(world.rotation)),
            scale = world.scale * inv_s
        };
    }

    // X=pitch, Y=yaw, Z=roll (degrees)
    static Quaternion EulerToQuat(Vector3 euler_deg)
    {
        float deg2rad = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(euler_deg.Y * deg2rad, euler_deg.X * deg2rad, euler_deg.Z * deg2rad);
    }

    static Vector3 QuatToEuler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float sinp = 2f * (q.W * q.X - q.Z * q.Y);
        float pitch, yaw, roll;
        if (MathF.Abs(sinp) >= 1f)
            pitch = MathF.CopySign(MathF.PI / 2f, sinp);
        else
            pitch = MathF.Asin(sinp);
        yaw = MathF.Atan2(2f * (q.W * q.Y + q.Z * q.X), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
        float rad2deg = 180f / MathF.PI;
        return new Vector3(pitch * rad2deg, yaw * rad2deg, roll * rad2deg);
    }
}