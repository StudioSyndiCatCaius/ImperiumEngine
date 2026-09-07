using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Interfaces;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Core;

public class Imp2D : ImpComp
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static List<Imp2D> cursortrace_stack = new();
    public static float draw_opacity = 1f;

    public static Imp2D GetCursorTraceHit(Vector2 point) //might need to reverse?
    {
        foreach (Imp2D comp in cursortrace_stack)
        {
            if (comp.Contains(point)) return comp;
        }
        return null;
    }

    public static Color DrawTint(Color c)
    {
        float a = draw_opacity;
        if (a >= 1f) return c;
        if (a <= 0f) return new Color(c.R, c.G, c.B, (byte)0);
        return new Color(c.R, c.G, c.B, (byte)(c.A * a));
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    // ==========================================================================
    // Imp Vars
    // ==========================================================================
    [ImpVar] public TLayout2 layout=new()
    {
        size = {X=100,Y=100}
    };
    [ImpVar] public float opacity=1.0f;
    [ImpVar] public Vector2 scale=Vector2.One;
    [ImpVar] public ECursorFilter cursor_filter=ECursorFilter.Pass; //applied when cursor is in the bounds of this component
    
    // ==========================================================================
    // vars
    // ==========================================================================
    public ImpViewport? owning_viewport; //override only (C3_UI plane, etc). otherwise inherited / scene / App.viewport_main
        
    public TBounds2 bounds; //cached bounds for 2d drawing
    float _visual_op_prev;
    bool _visual_scaled;

    protected override ECompProcess ProcessKinds => ECompProcess.Update | ECompProcess.Draw2D | ECompProcess.Cursor;
    
    // ==========================================================================
    // init
    // ==========================================================================
    
    // ==========================================================================
    // funcs
    // ==========================================================================

    public override void ProcessNotify(ENotifyProcess notify, double dt)
    {
        if (notify == ENotifyProcess.Draw2D)
        {
            if (is_visible) Visual_Push();
            base.ProcessNotify(notify, dt);
            if (is_visible) Visual_Pop();
            return;
        }
        if (notify == ENotifyProcess.CursorStack)
        {
            if(!is_visible) return;
            bool _do_children=false;
            bool _do_self=false;
            switch (cursor_filter)
            {
                case ECursorFilter.Pass:
                    _do_children=true;
                    break;
                case ECursorFilter.Hit:
                    _do_self = true;
                    _do_children=true;
                    break; 
                case ECursorFilter.Ignore:
                    break;
                case ECursorFilter.Block:
                    _do_self=true;
                    break;
            }

            if (_do_self)
            {
                cursortrace_stack.Add(this);
            }

            if (_do_children)
            {
                foreach (ImpComp child in children)
                {
                    if (child is Imp2D child2d) child2d.ProcessNotify(notify, dt);
                }
            }
            
            return;
        }
        base.ProcessNotify(notify, dt);
    }
    
    
    public ImpViewport Viewport_Get()
    {
        if (owning_viewport != null) return owning_viewport;
        if (parent is Imp2D p) return p.Viewport_Get();
        if (scene?.viewport != null) return scene.viewport;
        return App.viewport_main;
    }

    public void Visual_Push()
    {
        bounds = Bounds_Cache();
        _visual_op_prev = draw_opacity;
        float o = opacity;
        if (o < 0f) o = 0f;
        else if (o > 1f) o = 1f;
        draw_opacity = _visual_op_prev * o;

        _visual_scaled = scale.X != 1f || scale.Y != 1f;
        if (!_visual_scaled) return;

        float x0 = MathF.Min(bounds.start.X, bounds.end.X);
        float y0 = MathF.Min(bounds.start.Y, bounds.end.Y);
        float w = MathF.Abs(bounds.end.X - bounds.start.X);
        float h = MathF.Abs(bounds.end.Y - bounds.start.Y);
        Vector2 pivot = bounds.start;
        if (!bounds.IsEmpty)
            pivot = new Vector2(
                x0 + (layout.anchor_normalized ? layout.anchor_position.X * w : layout.anchor_position.X),
                y0 + (layout.anchor_normalized ? layout.anchor_position.Y * h : layout.anchor_position.Y));
        Rlgl.PushMatrix();
        Rlgl.Translatef(pivot.X, pivot.Y, 0);
        Rlgl.Scalef(scale.X, scale.Y, 1);
        Rlgl.Translatef(-pivot.X, -pivot.Y, 0);
    }

    public void Visual_Pop()
    {
        if (_visual_scaled) Rlgl.PopMatrix();
        draw_opacity = _visual_op_prev;
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
        bounds = Bounds_Cache();
    }

    public virtual TBounds2 ContentBounds() { return bounds; }
    public virtual TBounds2 Child_MakeBounds2D(ImpComp child, int index) { return bounds; }

    
    
    public virtual bool ChildLayout_IsFree() { return true; } //TRUE=allows children to be freely moved around. FALSE= `Child_MakeBounds2D` determines child bounds
    

    public virtual TBounds2 Bounds_Cache()
    {
        TBounds2 slot;
        if (parent is Imp2D p && !p.ChildLayout_IsFree())
        {
            int ind = (uint)sibling_index < (uint)p.children.Count && p.children[sibling_index] == this
                ? sibling_index
                : -1;
            if (ind < 0) return bounds;
            slot = p.Child_MakeBounds2D(this, ind);
        }
        else if (parent is Imp2D pp && !pp.bounds.IsEmpty)
            slot = pp.ContentBounds();
        else
            slot = Viewport_Get().Bounds;
        return layout.MakeBounds(slot);
    }
    
    public bool Contains(Vector2 point) { return bounds.IsPointInside(point); }

    protected override void Transform_Refresh()
    {
        bounds = Bounds_Cache();
        if (ChildLayout_IsFree()) return;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is Imp2D child2d)
                child2d.bounds = child2d.Bounds_Cache();
        }
    }
    
    
    // ---------------------------------------
    // As Option (when used in an C2_OptionList)
    // ---------------------------------------
    [ImpVar][Category("Option")] public C2_Button option_button;
    [ImpVar][Category("Option")] public C2_Text option_title_text;
    [ImpVar][Category("Option")] public C2_Text option_description_text;
    [ImpVar][Category("Option")] public C2_Image option_icon;
    
    public object option_data;

    public virtual void Option_Refresh()
    {
        if (option_data is I_General _dat)
        {
            if (option_title_text != null) option_title_text.text = _dat.getTitle() ?? "";
            if (option_description_text != null) option_description_text.text = _dat.getDescription() ?? "";
            if (option_icon != null) option_icon.texture = _dat.getIcon();
        }
        OnOption_Refreshed();
    }
    
    public virtual void OnOption_Added(C2_OptionList list, int index) { }
    public virtual void OnOption_Refreshed() { }
    public virtual void OnOption_DataChanged(object new_data) { }
    public virtual void OnOption_ListContextChanged(C2_OptionList list) { }
}
