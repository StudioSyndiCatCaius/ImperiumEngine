using Engine.Core;
using Engine.Enums;
using Engine.Assets;
using Engine.Structs;

namespace Engine.Comps._2D;

[ImpClass(Common = true)][Title("Text")]
public class C2_Text : Imp2D
{
    // =============================================================================
    // ImpVar
    // =============================================================================
    [ImpVar] public A_Font font=A_Font.ARIAL_P;
    [ImpVar] public string text;
    [ImpVar] public ETextWrap wrap = ETextWrap.Word;
    [ImpVar] public TLayoutAlignment text_align = TLayoutAlignment.TOP_LEFT;
    
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
        base.OnDraw2D(dt, flags);
        A_Font fnt = font ?? A_Font.ARIAL_P;
        if (string.IsNullOrEmpty(text)) return;
        fnt.Draw(text, bounds, wrap, text_align);
    }
}
