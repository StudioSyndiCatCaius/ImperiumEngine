using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Interfaces;
using Engine.Structs;

namespace Engine.Core;

public class Imp2D : ImpComp
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static List<Imp2D> cursortrace_stack = new();

    public static Imp2D GetCursorTraceHit(Vector2 point) //might need to reverse?
    {
        foreach (Imp2D comp in cursortrace_stack)
        {
            if (comp.Contains(point)) return comp;
        }
        return null;
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
    [ImpVar] public ECursorFilter cursor_filter=ECursorFilter.Pass; //applied when cursor is in the bounds of this component
    
    [ImpVar] public TTransform2 transform; //UNLIKE 3D, 2D transforms are relative to the parent. transform is an additional offset on top of that.
    
    // ==========================================================================
    // vars
    // ==========================================================================
    public ImpViewport? owning_viewport; //override only (C3_UI plane, etc). otherwise inherited / scene / App.viewport_main
        
    public TTransform2 global_transform;
    public TBounds2 bounds; //cached bounds for 2d drawing
    
    // ==========================================================================
    // init
    // ==========================================================================
    
    // ==========================================================================
    // funcs
    // ==========================================================================

    public override void ProcessNotify(ENotifyProcess notify, double dt)
    {
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

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
        bounds = TBounds2.GetWindowBounds();
        if (parent != null && parent.children.Contains(this))
        {
            if (parent is Imp2D p)
            {
                int _ind=p.children.IndexOf(this);
                if (_ind >= 0)
                {
                    bounds = p.Child_MakeBounds2D(this, _ind);
                }
            }
        }
        bounds=layout.MakeBounds(bounds);
    }

    public virtual TBounds2 ContentBounds() { return bounds; }
    public virtual TBounds2 Child_MakeBounds2D(ImpComp child, int index) { return bounds; }

    
    
    public virtual bool ChildLayout_IsFree() { return true; } //TRUE=allows children to be freely moved around. FALSE= `Child_MakeBounds2D` determines child bounds
    

    public virtual TBounds2 Bounds_Cache()
    {
        TBounds2 slot = parent is Imp2D p && !p.bounds.IsEmpty
            ? p.ContentBounds()
            : Viewport_Get().Bounds;
        TBounds2 b = layout.MakeBounds(slot);
        b.start += transform.position;
        b.end += transform.position;
        return b;
    }
    
    public bool Contains(Vector2 point) { return bounds.IsPointInside(point); }

    // ---------------------------------------
    // Transform (Set Global)
    // ---------------------------------------
    public void Transform_Set(TTransform2 t, bool global = true)
    { if (global) global_transform = t; else transform = t; Correct_Transform(global); }
    public void Position_Set(Vector2 position, bool global = true)
    { if (global) global_transform.position = position; else transform.position = position; Correct_Transform(global); }
    public void Rotation_Set(float rotation, bool global = true)
    { if (global)global_transform.rotation = rotation; else transform.rotation = rotation; Correct_Transform(global); }
    public void Scale_Set(Vector2 scale, bool global = true) { if (global) global_transform.scale = scale; else transform.scale = scale; Correct_Transform(global); }
    protected override void Transform_Refresh()
    {
        Correct_Transform(false);
        if (ChildLayout_IsFree()) return;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is Imp2D child2d)
                child2d.bounds = Child_MakeBounds2D(child2d, i);
        }
    }

    void Correct_Transform(bool global_changed)
    {
        Imp2D parent2d = parent as Imp2D;
        if (parent2d == null)
        {
            if (global_changed) transform = global_transform;
            else global_transform = transform;
        }
        else if (global_changed) transform = TTransform2.Subtract(global_transform, parent2d.global_transform);
        else global_transform = TTransform2.Add(parent2d.global_transform, transform);

        if (parent2d == null || parent2d.ChildLayout_IsFree())
            bounds = Bounds_Cache();
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
        I_General _dat=option_data as I_General;
        if (_dat != null)
        {
            string _title = _dat.getTitle().ToString();
            string _description = _dat.getDescription().ToString();
            A_Texture _icon = _dat.getIcon();
            if (option_title_text != null) option_title_text.text = _title;
            if (option_description_text != null) option_description_text.text = _description;
            if (option_icon != null) option_icon.texture = _icon;
        }
        OnOption_Refreshed();
    }
    
    public virtual void OnOption_Added(C2_OptionList list, int index) { }
    public virtual void OnOption_Refreshed() { }
    public virtual void OnOption_DataChanged(object new_data) { }
    public virtual void OnOption_ListContextChanged(C2_OptionList list) { }
}
