using Engine.Core;
using Engine.Enums;
using Engine.Assets;
using Engine.Structs;

namespace Engine.Comps._2D;

[ImpClass(Common = true)]
public class C2_Text : Imp2D
{
    // =============================================================================
    // ImpVar
    // =============================================================================
    [ImpVar] public A_Font font=A_Font.ARIAL_P;
    [ImpVar] public string text;
    [ImpVar] public ETextWrap wrap = ETextWrap.Word;
    
    // =============================================================================
    // INIT
    // =============================================================================
    public C2_Text() {}
    public C2_Text(string _text) { text = _text; }
    
    // =============================================================================
    // Overrides
    // =============================================================================
    public override bool ChildLayout_IsFree() { return false; }

    public override void OnDraw2D(double dt, EDrawFlags flags = 0)
    {
        A_Font fnt = font ?? A_Font.ARIAL_P;
        if (string.IsNullOrEmpty(text)) return;
        fnt.Draw(text, bounds, new TTransform2 { position = Pivot_Pixels(), rotation = global_transform.rotation }, wrap);
    }
}
