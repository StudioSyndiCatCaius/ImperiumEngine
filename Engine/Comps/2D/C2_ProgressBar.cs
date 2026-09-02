using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Comps._2D;

public enum EProgressBarLayout
{
    H_Right_2_Left,
    H_Left_2_Right, 
    V_Top_2_Bottom, 
    V_Bottom_2_Top,
}

public class C2_ProgressBar : Imp2D
{
    [ImpVar] public float progress;
    [ImpVar] public EProgressBarLayout layout;
    [ImpVar] public UI_ProgressBar style;

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);
        TMargins progress_clipping_margins=new(); //calc based on layout and progress
        
        style.background_texture.Draw(bounds, global_transform, EImageLayout.Stretch, style.background_nineslice_margins);
        style.progress_texture.Draw(bounds, global_transform, EImageLayout.Stretch, style.progress_nineslice_margins,progress_clipping_margins, true);
    }
}


public class UI_ProgressBar : ImpAsset
{
    [ImpVar] public A_Texture background_texture;
    [ImpVar] public TMargins background_nineslice_margins;
    [ImpVar] public A_Texture progress_texture;
    [ImpVar] public TMargins progress_nineslice_margins;
    
}
