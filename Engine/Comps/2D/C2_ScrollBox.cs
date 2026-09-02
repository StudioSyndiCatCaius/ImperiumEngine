using Engine.Core;

namespace Engine.Comps._2D;

public class C2_ScrollBox : Imp2D
{
    [ImpVar] public UI_ScrollBox style;
}

public class UI_ScrollBox : ImpAsset
{
    [ImpVar] public UI_Box box_content;
    [ImpVar] public UI_Box scoll_background;
    [ImpVar] public UI_Box scoll_bar;
}