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
        
        for (int i = 0; i < children.Count; i++)
            children[i].is_visible = i == current_tab;
        
        bound_bar=default;
        bound_body=bounds;
        if (show_tabs)
        {
            float size_bar=50;
            bounds.Split2(size_bar, bounds.Size.Y - size_bar, false, EUIOrentation.V, out bound_bar, out bound_body);
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
        if(style.body_background != null) style.body_background.Draw(bound_body, new TTransform2());
        
        if (show_tabs)
        {
        }
    }
}

public class UI_TabContainer : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [ImpVar] public UI_Box body_background=UI_Box.DARK;
    
    [ImpVar] public UI_Box tabs_background=UI_Box.DARK;
    [ImpVar] public A_Font tabs_font=A_Font.ARIAL_P;
    
    [ImpVar] public UI_Box tab_idle;
    [ImpVar] public UI_Box tab_current;
    [ImpVar] public UI_Box tab_hovered;
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [ImpVar] public static UI_TabContainer DEFAULT = new();
    [ImpVar] public static UI_TabContainer BLANK = new()
    {
        body_background=UI_Box.BLANK,
    };
}