using Engine.Assets;
using Engine.Core;

namespace Engine.Comps._2D;

public class C2_TextEdit : Imp2D
{
    [ImpVar] public UI_TextEdit style;
    [ImpVar] public string text;
    [ImpVar] public string placeholder;
    [ImpVar] public bool multiline;
    
    public override bool ChildLayout_IsFree() { return false; }
}



public class UI_TextEdit : ImpAsset
{
    [ImpVar] public UI_Box box;
    [ImpVar] public A_Flow font_text;
    [ImpVar] public A_Flow font_placeholder;
}