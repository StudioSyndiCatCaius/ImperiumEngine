using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Main;

[Flags]
public enum EDrawFlags
{
    None       = 0,
    WithEditor = 1 << 0, //drawing into an editor viewport: gizmos, helpers, selection
    DebugDraw  = 1 << 1, //debug overlays: colliders, bounds, nav
}

public enum ECompUpdateMode
{
    Inherit, // inherit from parent
    On, // force update On, regardless of parent
    Off, // force update Off, regardless of parent
}



//comps/components are the building blocks of Imperium Node objects

public class ImpComp : I_InputTarget, I_General
{
    // ========================================================================
    // Statics
    // ========================================================================

    public A_Texture? gIcon() => ImpIcon.Get(GetType());

    
    // ========================================================================
    // Class
    // ========================================================================

    public Guid Guid;
    public string name;
    public ImpComp parent=null;
    public List<ImpComp> children = new List<ImpComp>();
    public ImpScene owning_scene;
    
    [ImpVar] public bool is_visible = true;
    [ImpVar] public A_Script script;
    [ImpVar] public ECompUpdateMode update_mode = ECompUpdateMode.Inherit;

    //allows for exposing & editing instances of this comps' children. when turning off, reinit chidlren to refualt before rer
    [ImpVar]
    public bool children_editable
    {
        set { children_editable=value;
            if (value)
            {
              // amke children editable  
            }
            else
            {
                //make them uneditable
            }
        }
    } 

    public void Destroy()
    {
        parent?.children.Remove(this);
        parent = null;
        owning_scene?.on_comp_removed?.Invoke(owning_scene, this);
        //queue for destruction/remove from memory
    }
    
    public void Child_Add(ImpComp Child)
    {
        if (Child.parent != null)
        {
            Child.parent.children.Remove(Child);
        }
        children.Add(Child);
        Child.parent = this;
        owning_scene?.on_comp_added?.Invoke(owning_scene, Child);
    }

    public void Child_Remove(ImpComp Child)
    {
        if (Child.parent == this) Child.Destroy();
    }

    // Detaches the list before destroying: Destroy() pulls the child out of its parent's
    // `children`, so destroying while enumerating that same list invalidates the iterator.
    public void Child_RemoveAll()
    {
        var doomed = children.ToArray();
        children.Clear();

        foreach (var child in doomed) { child.Destroy(); }
    }
    
    // ---------------------------------------------------
    // virtuals
    // ---------------------------------------------------
    
    // Editor + Runtime -----------------
    [ImpFunc] public virtual void OnInit()
    {
        foreach (var child in children) { child.OnInit(); }
    }
    
    [ImpFunc] public virtual void OnDeinit()
    {
        foreach (var child in children) { child.OnDeinit(); }
    }
    
    // Computes screen rectangles for the subtree. Runs before input and draw so both
    // have real geometry to work with. Non-2D comps just pass the area straight down.
    public virtual void OnLayout(Rectangle area)
    {
        if(!is_visible) return;
        foreach (var child in children)
        {
            if (!child.is_visible) continue;
            child.OnLayout(area);
        }
    }

    public virtual void OnDraw(double dt, EDrawFlags flags)
    {
        if(!is_visible) return;
        foreach (var child in children)
        {
            if (!child.is_visible) continue;
            child.OnDraw(dt, flags);
        }
    }

    // RUNTIME -----------------
    [ImpFunc] public virtual void OnBegin()
    {
        foreach (var child in children) { child.OnBegin(); }
    }

    [ImpFunc] public virtual void OnEnd()
    {
        foreach (var child in children) { child.OnEnd(); }
    }
    
    [ImpFunc] public virtual void OnUpdate(double dt)
    {
        foreach (var child in children)
        {
            child.owning_scene = owning_scene;
            child.OnUpdate(dt);
        }
    }
    

    // ---------------------------------------------------
    // mouse
    // ---------------------------------------------------
    
    public virtual bool Mouse_IsOver()
    {
        return false;
    }
    
    // ---------------------------------------------------
    // cursor --- NOTE: Mouse and cursor are usually the same thing, but cursor works for virtual cursors as well (like on console)
    // ---------------------------------------------------
    
    
    public virtual bool Cursor_IsOver()
    {
        return false;
    }
    
    public virtual void Cursor_OnEntry(bool enter)
    {
        
    }
    
    
    public virtual void Cursor_OnEvent(ECursorEvent ev)
    {

    }

    // Wheel events bubble from the hovered comp up to the first ancestor that says yes,
    // so scrolling works while the cursor is over a list item rather than the scroll box.
    public virtual bool Cursor_WantsWheel()
    {
        return false;
    }
}

// =====================================================================================
// 2D
// =====================================================================================

public class ImpComp2D : ImpComp
{
    // ================================================================================================
    // Statics
    // // ================================================================================================
    
    
    // ================================================================================================
    // Class
    // ================================================================================================
    // = new() matters: the parameterless ctor is what sets scale to one, and a bare struct
    // field would otherwise default to a zero scale.
    [ImpVar] public TTransform2 transform = new();
    [ImpVar] public Vector2 size = Vector2.Zero; // size of the 2d component. this is dependant on the anchor preset.
    
    [ImpVar] public Vector2 size_min = Vector2.Zero;
    [ImpVar] public Vector2 size_max =new Vector2(-1, -1);
    
    [ImpVar] public bool propagate_size = true;
    [ImpVar] public bool clip_contents;
    [ImpVar] public EUIAnchorPreset anchor_preset = EUIAnchorPreset.TopLeft;
    [ImpVar] public TUISizing sizing_horizontal;
    [ImpVar] public TUISizing sizing_vertical;
    [ImpVar] public float expand_ratio=1.0f;
    [ImpVar] public ECursorFilter cursor_filter;

    //theme for this comp AND its descendants; null inherits from the nearest ancestor
    [ImpVar] public ImpUITheme? theme;


    [ImpVar] public Color modulate = Color.White;
    [ImpVar] public Color modulate_self = Color.White; // modulate the color of the component itself, not the children.

    // ---------------------------------------------------
    // Option
    // ---------------------------------------------------

    
    [ImpVar][Category("Option")] public Object option_source; //a generic object to reference generic data from
    [ImpVar][Category("Option")] public C2_Text? option_ui_title;
    [ImpVar][Category("Option")] public C2_Rect? option_ui_icon;
    [ImpVar][Category("Option")] public C2_Text? option_ui_description;
    [ImpVar][Category("Option")] public C2_Button? option_ui_button; // button to trigger select/hover on this
    
    public Action<ImpComp2D> on_option_select;
    public Action<ImpComp2D, bool> on_option_hover;

    public void Option_Refresh()
    {
        if (option_source == null) return;
        if (option_source is I_General _s)
        {
            option_ui_title.text = _s.gTitle().ToString();
            option_ui_icon.Set_FromTexture(_s.gIcon());
            option_ui_description.text = _s.gDescription().ToString();    
        }

        if (option_ui_button!=null)
        {
            option_ui_button.on_click = (btn) => on_option_select.Invoke(this);
        }
        
    }
    
    // ---------------------------------------------------
    // misc
    // ---------------------------------------------------

    
    // Absolute screen rect for this comp, recomputed every frame by OnLayout.
    public Rectangle rect;
    // rect inset by the inner margins: the area this comp's children lay out into.
    public Rectangle rect_content;

    // Smallest size this comp needs for its own content. Leaves measure their content
    // (text, label + padding); containers derive it from children. Drives the Shrink*
    // sizing presets and auto-sizing when `size` is left at zero.
    public virtual Vector2 Size_GetContentMin() => Vector2.Zero;

    //explicit layout margins; when null the comp's style supplies them
    public TMargins? margins_inner;
    public TMargins? margins_outer;

    public TMargins Margins_GetInner() => margins_inner ?? Margins_StyleInner();
    public TMargins Margins_GetOuter() => margins_outer ?? Margins_StyleOuter();

    // Comps with a UIStyle_Rect override these to take margins from it.
    protected virtual TMargins Margins_StyleInner() => default;
    protected virtual TMargins Margins_StyleOuter() => default;

    // ---------------------------------------------------
    // theme
    // ---------------------------------------------------

    // Nearest theme up the tree, falling back to the app default. This is the cascade:
    // setting `theme` on any comp restyles that entire subtree. Resolved per draw rather
    // than cached, so swapping a theme takes effect on the next frame.
    public ImpUITheme Theme_Get()
    {
        for (ImpComp? c = this; c != null; c = c.parent)
        {
            if (c is ImpComp2D c2 && c2.theme != null) return c2.theme;
        }

        return ImpUITheme.game_theme;
    }
    


    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    public override void OnLayout(Rectangle area)
    {
        if (!is_visible) return;

        rect = Rect_Resolve(area);
        rect_content = ImpUI.Rect_Inset(rect, Margins_GetInner());

        Layout_Children(rect_content);
    }

    // Containers override this to place children themselves (lists, tabs, menu bars).
    // By default each child anchors independently inside the content area.
    protected virtual void Layout_Children(Rectangle content)
    {
        foreach (var child in children)
        {
            if (!child.is_visible) continue;
            child.OnLayout(content);
        }
    }

    // Lays this comp out into an exact rect, skipping anchor resolution. Containers that
    // position children themselves (lists, scroll boxes) use this instead of OnLayout.
    public void OnLayout_Exact(Rectangle exact)
    {
        rect = exact;
        rect_content = ImpUI.Rect_Inset(rect, Margins_GetInner());

        Layout_Children(rect_content);
    }

    // Wipes resolved geometry for a subtree. A container that chooses not to lay out a
    // child this frame must call this, or the child keeps last frame's rect and stays
    // hit-testable while invisible.
    public static void Rect_Clear(ImpComp comp)
    {
        if (comp is ImpComp2D c2)
        {
            c2.rect = default;
            c2.rect_content = default;
        }
        foreach (var child in comp.children) { Rect_Clear(child); }
    }

    // Resolves anchor_preset + size + margins into an absolute rect inside `area`.
    // `area` is the parent's content rect, never the viewport, so nesting works.
    public Rectangle Rect_Resolve(Rectangle area)
    {
        Anchor_Get(anchor_preset, out var a_min, out var a_max);

        area = ImpUI.Rect_Inset(area, Margins_GetOuter());
        var content_min = Size_GetContentMin();

        Axis_Resolve(area.X, area.Width, a_min.X, a_max.X, size.X, content_min.X,
                     transform.position.X, size_min.X, size_max.X, out var x, out var w);
        Axis_Resolve(area.Y, area.Height, a_min.Y, a_max.Y, size.Y, content_min.Y,
                     transform.position.Y, size_min.Y, size_max.Y, out var y, out var h);

        return new Rectangle(x, y, w, h);
    }

    // One axis of the anchor solve.
    //   anchors equal  -> point-anchored: length comes from `want` (or content), and the
    //                     anchor fraction doubles as the alignment (0 lead, .5 centre, 1 trail)
    //   anchors differ -> stretch-anchored: the axis spans that fraction of the parent
    static void Axis_Resolve(float area_pos, float area_len, float a_min, float a_max,
                             float want, float content_min, float offset,
                             float len_min, float len_max,
                             out float pos, out float len)
    {
        float p_min = area_pos + area_len * a_min;
        float p_max = area_pos + area_len * a_max;

        if (a_min == a_max)
        {
            len = want > 0 ? want : content_min;
            pos = p_min - len * a_min;
        }
        else
        {
            len = p_max - p_min;
            pos = p_min;
        }

        len = MathF.Max(len, len_min);
        if (len_max >= 0) len = MathF.Min(len, len_max);

        pos += offset;
    }

    // Maps a preset onto normalised anchor corners in parent space.
    public static void Anchor_Get(EUIAnchorPreset preset, out Vector2 a_min, out Vector2 a_max)
    {
        switch (preset)
        {
            case EUIAnchorPreset.Full:         a_min = new(0f, 0f);    a_max = new(1f, 1f);    break;

            case EUIAnchorPreset.TopLeft:      a_min = new(0f, 0f);    a_max = a_min;          break;
            case EUIAnchorPreset.TopRight:     a_min = new(1f, 0f);    a_max = a_min;          break;
            case EUIAnchorPreset.BottomLeft:   a_min = new(0f, 1f);    a_max = a_min;          break;
            case EUIAnchorPreset.BottomRight:  a_min = new(1f, 1f);    a_max = a_min;          break;

            case EUIAnchorPreset.Center:       a_min = new(0.5f, 0.5f); a_max = a_min;         break;
            case EUIAnchorPreset.CenterLeft:   a_min = new(0f, 0.5f);   a_max = a_min;         break;
            case EUIAnchorPreset.CenterRight:  a_min = new(1f, 0.5f);   a_max = a_min;         break;
            case EUIAnchorPreset.CenterTop:    a_min = new(0.5f, 0f);   a_max = a_min;         break;
            case EUIAnchorPreset.CenterBottom: a_min = new(0.5f, 1f);   a_max = a_min;         break;

            case EUIAnchorPreset.WideLeft:     a_min = new(0f, 0f);     a_max = new(0f, 1f);   break;
            case EUIAnchorPreset.WideRight:    a_min = new(1f, 0f);     a_max = new(1f, 1f);   break;
            case EUIAnchorPreset.WideTop:      a_min = new(0f, 0f);     a_max = new(1f, 0f);   break;
            case EUIAnchorPreset.WideBottom:   a_min = new(0f, 1f);     a_max = new(1f, 1f);   break;
            case EUIAnchorPreset.WideCenterH:  a_min = new(0f, 0.5f);   a_max = new(1f, 0.5f); break;
            case EUIAnchorPreset.WideCenterV:  a_min = new(0.5f, 0f);   a_max = new(0.5f, 1f); break;

            default:                           a_min = new(0f, 0f);     a_max = a_min;         break;
        }
    }
    
    // ---------------------------------------------------
    // Life
    // ---------------------------------------------------

    public override void OnBegin()
    {
        base.OnBegin();
        Option_Refresh();
    }

    public override void OnEnd()
    {
        base.OnEnd();
    }

    // ---------------------------------------------------
    // mouse / cursor
    // ---------------------------------------------------

    // Plain geometry: is the pointer inside this comp's rect right now. Says nothing about
    // who the cursor was actually routed to, so a comp buried under a panel still answers
    // true. Use Cursor_IsOver when occlusion matters.
    public override bool Mouse_IsOver()
    {
        if (!is_visible) return false;
        if (ImpUI.Overlay_IsBlocking(ImpUI.mouse_pos)) return false;

        return Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect);
    }

    // The routed cursor: true when this frame's hit test landed on this comp or on
    // anything beneath it, so a container reads as hovered while its children are.
    public override bool Cursor_IsOver()
    {
        for (ImpComp? c = ImpUI.hovered; c != null; c = c.parent)
        {
            if (c == this) return true;
        }

        return false;
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    public override void OnDraw(double dt, EDrawFlags flags)
    {
        if (!is_visible) return;

        // modulate tints this comp and its children; modulate_self tints only this comp
        ImpUI.Modulate_Push(modulate);

        ImpUI.Modulate_Push(modulate_self);
        Draw_Self(dt, flags);
        ImpUI.Modulate_Pop();

        if (clip_contents) ImpUI.Clip_Push(rect_content);
        Draw_Children(dt, flags);
        if (clip_contents) ImpUI.Clip_Pop();

        ImpUI.Modulate_Pop();
    }

    // Comps override this to paint themselves. Children are drawn separately so the
    // two modulate colours apply to the right things.
    protected virtual void Draw_Self(double dt, EDrawFlags flags) { }

    protected virtual void Draw_Children(double dt, EDrawFlags flags)
    {
        foreach (var child in children)
        {
            if (!child.is_visible) continue;
            child.OnDraw(dt, flags);
        }
    }
}

// =====================================================================================
// 3D
// =====================================================================================

public class ImpComp3D : ImpComp
{
    // =====================================================================================
    // Statics
    // =====================================================================================
    

    // =====================================================================================
    // Game
    // =====================================================================================

    // = new() matters: the parameterless ctor is what sets scale to one, and a bare struct
    // field would otherwise default to a zero scale, collapsing everything drawn with it.
    [ImpVar] public TTransform3 transform = new();
    [ImpVar] public bool physics_enabled = false; // indicates physics is enabled on this

    // ---------------------------------------------
    // Transform
    // ---------------------------------------------

    public TTransform3 Transform_Get(bool global)
    {
        if (!global) return transform;

        if (!Matrix4x4.Decompose(Matrix_GetWorld(), out var scale, out var rotation, out var position))
            return transform;

        return new TTransform3
        {
            position = position,
            rotation = Imp3D.Euler_FromQuat(rotation),
            scale = scale,
        };
    }

    public void Transform_Set(TTransform3 _transform, bool global)
    {
        if (!global)
        {
            transform = _transform;
            return;
        }

        // Express the wanted world transform in the parent's space, so setting a global
        // transform on a parented comp lands where the caller asked for.
        var world = Matrix_Compose(_transform);
        if (Matrix4x4.Invert(Matrix_GetParentWorld(), out var to_local)) world *= to_local;

        if (!Matrix4x4.Decompose(world, out var scale, out var rotation, out var position)) return;

        transform.position = position;
        transform.rotation = Imp3D.Euler_FromQuat(rotation);
        transform.scale = scale;
    }

    // Matrix ----------------

    public Matrix4x4 Matrix_GetLocal() => Matrix_Compose(transform);

    // This comp's transform composed with every 3D ancestor's, so a comp moves with
    // whatever it is parented to. Non-3D ancestors (a scene root, a viewport) contribute
    // nothing and are simply walked past.
    public Matrix4x4 Matrix_GetWorld() => Matrix_GetLocal() * Matrix_GetParentWorld();

    Matrix4x4 Matrix_GetParentWorld()
    {
        var matrix = Matrix4x4.Identity;
        for (ImpComp? c = parent; c != null; c = c.parent)
        {
            if (c is ImpComp3D c3) matrix *= c3.Matrix_GetLocal();
        }

        return matrix;
    }

    static Matrix4x4 Matrix_Compose(TTransform3 t)
    {
        return Matrix4x4.CreateScale(t.scale)
             * Matrix4x4.CreateFromQuaternion(Imp3D.Quat_FromEuler(t.rotation))
             * Matrix4x4.CreateTranslation(t.position);
    }


    // ---------------------------------------------------
    // virtuals
    // ---------------------------------------------------
    
    //Gets this bounding
    public Vector3 Bounds_GetExtent()
    {
        return Vector3.Zero;
    }
    
    // Overlap ----------------
    public void Overlap_Begin(ImpComp3D other)
    {
        
    }
    
    public void Overlap_End(ImpComp3D other)
    {
        
    }
    
}