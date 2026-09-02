using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Interfaces;
using Engine.Structs;

namespace Engine.Core;

public class Imp2D : ImpComp
{
    // ===========================================================================================
    // Imp Vars
    // ===========================================================================================
    [ImpVar] public TLayout2 layout=new()
    {
        size = {X=100,Y=100}
    };
    [ImpVar] public bool pivot_normalized=true;
    [ImpVar] public Vector2 pivot=new(0.5f,0.5f);
    [ImpVar] public ECursorFilter cursor_filter=ECursorFilter.Pass; //applied when cursor is in the bounds of this component
    
    [ImpVar] public TTransform2 transform; //UNLIKE 3D, 2D transforms are relative to the parent. transform is an additional offset on top of that.
    
    // ===========================================================================================
    // vars
    // ===========================================================================================
    public ImpViewport? owning_viewport; //override only (C3_UI plane, etc). otherwise inherited / scene / App.viewport_main
        
    public TTransform2 global_transform;

    
    // ===========================================================================================
    // init
    // ===========================================================================================
    
    // ===========================================================================================
    // funcs
    // ===========================================================================================
    public ImpViewport Viewport_Get()
    {
        if (owning_viewport != null) return owning_viewport;
        if (parent is Imp2D p) return p.Viewport_Get();
        if (scene?.viewport != null) return scene.viewport;
        return App.viewport_main;
    }

    public virtual TBounds2 ContentBounds() { return bounds; }
    
    [CallInEditor] public void SetPivot_Corner() { pivot = new(0,0); pivot_normalized = false; }
    [CallInEditor] public void SetPivot_Center() { pivot = new(0.5f,0.5f); pivot_normalized = true; }

    public Vector2 Pivot_Pixels()
    {
        Vector2 size = new(
            MathF.Abs(bounds.end.X - bounds.start.X),
            MathF.Abs(bounds.end.Y - bounds.start.Y));
        return pivot_normalized ? pivot * size : pivot;
    }

    public Vector2 Pivot_Point()
    {
        return new Vector2(
            MathF.Min(bounds.start.X, bounds.end.X),
            MathF.Min(bounds.start.Y, bounds.end.Y)) + Pivot_Pixels();
    }

    public bool Contains(Vector2 point)
    {
        if (bounds.IsEmpty) return false;
        Vector2 origin = new(
            MathF.Min(bounds.start.X, bounds.end.X),
            MathF.Min(bounds.start.Y, bounds.end.Y));
        Vector2 size = new(
            MathF.Abs(bounds.end.X - bounds.start.X),
            MathF.Abs(bounds.end.Y - bounds.start.Y));
        float rot = (float)global_transform.rotation;
        if (MathF.Abs(rot) > 1e-4f)
        {
            Vector2 o = origin + Pivot_Pixels();
            float rad = -rot * (MathF.PI / 180f);
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            Vector2 d = point - o;
            point = o + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        }
        return point.X >= origin.X && point.Y >= origin.Y
            && point.X < origin.X + size.X && point.Y < origin.Y + size.Y;
    }

    public void Corners(Span<Vector2> corners)
    {
        Vector2 origin = new(
            MathF.Min(bounds.start.X, bounds.end.X),
            MathF.Min(bounds.start.Y, bounds.end.Y));
        Vector2 size = new(
            MathF.Abs(bounds.end.X - bounds.start.X),
            MathF.Abs(bounds.end.Y - bounds.start.Y));
        corners[0] = origin;
        corners[1] = new Vector2(origin.X + size.X, origin.Y);
        corners[2] = new Vector2(origin.X + size.X, origin.Y + size.Y);
        corners[3] = new Vector2(origin.X, origin.Y + size.Y);
        float rot = (float)global_transform.rotation;
        if (MathF.Abs(rot) <= 1e-4f) return;
        Vector2 o = origin + Pivot_Pixels();
        float rad = rot * (MathF.PI / 180f);
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        for (int i = 0; i < 4; i++)
        {
            Vector2 d = corners[i] - o;
            corners[i] = o + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        }
    }
    
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
