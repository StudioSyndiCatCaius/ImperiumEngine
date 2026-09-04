using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Comps._2D;

public class C2_TabContainer : Imp2D
{
    // =============================================================================
    // ImpVar
    // =============================================================================
    [ImpVar] public int current_tab;
    [ImpVar] public bool show_tabs = true;
    [ImpVar] public UI_TabContainer style;
        
    // =============================================================================
    // Actions
    // =============================================================================
    public Action<int> on_tab_changed;
    
    // =============================================================================
    // Vars
    // =============================================================================
    int _tab_prev=-1;
    TBounds2 bound_bar;
    TBounds2 bound_body;
        
    // =============================================================================
    // INIT
    // =============================================================================
    public C2_TabContainer(IEnumerable<Imp2D> _children, int _current_tab = 0)
    {
        children.AddRange(_children);
        current_tab = _current_tab;
    }

        
    // =============================================================================
    // Overrides
    // =============================================================================
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_tab_prev != current_tab)
        {
            _tab_prev=current_tab;
            on_tab_changed?.Invoke(current_tab);
        }
        
        foreach (var child in children)
        {
            int _index = children.IndexOf(child);
            if(_index == current_tab) child.is_visible = true;
            else child.is_visible = false;
        }
        
        bound_bar=default;
        bound_body=bounds;
        if (show_tabs)
        {
            float size_bar=50;
            float size_body=bounds.Size.Y-size_bar;
            TBounds2[] sections = bounds.Split([size_bar,size_body], false, EUIOrentation.V);
            bound_bar = sections[0];
            bound_body = sections[1];
        }
    }

    public override TBounds2 Child_MakeBounds2D(ImpComp child, int index)
    {
        return bound_body;
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
        if(style==null) return;
        if(style.body_background != null) style.body_background.Draw(bound_body, global_transform);
        
        if (show_tabs)
        {
            float[] tab_sections = new float[] { };
            foreach (var c in children)
            {
                int ind = children.IndexOf(c);
                tab_sections.SetValue(1,ind);
            }

            TBounds2[] tab_bounds = bound_bar.Split(tab_sections, true, EUIOrentation.H);
            foreach (var _bounds_4_tab in tab_bounds)
            {
                
            }
        }
    }
}

public class UI_TabContainer : ImpAsset
{
    [ImpVar] public UI_Box body_background=UI_Box.DARK;
    
    [ImpVar] public UI_Box tabs_background=UI_Box.DARK;
    [ImpVar] public A_Font tabs_font=A_Font.ARIAL_P;
    
    [ImpVar] public UI_Box tab_idle;
    [ImpVar] public UI_Box tab_current;
    [ImpVar] public UI_Box tab_hovered;
    
    public static UI_TabContainer DEFAULT = new();
}