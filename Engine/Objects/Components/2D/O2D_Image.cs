using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;


namespace ImperiumEngine.Objects._2D;

public class O2D_Image  : ImpComponent2D
{
    [ImpVar][Exposed] public TBrush_Image brush;
}