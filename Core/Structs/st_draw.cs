using System.Drawing;
using System.Numerics;

namespace ImperiumCore.Structs;

public class TComplexRectData
{
    [ImpVar][Exposed] Color color=Color.White;
    [ImpVar][Exposed] TVector2b cornerRadius;
    
    [ImpVar][Exposed] Color strokeColor;
    [ImpVar][Exposed] TVector2b strokeWidth;
    
    [ImpVar][Exposed] Color shadowColor;
    [ImpVar][Exposed] TVector2b shadowOffset;
    [ImpVar][Exposed] byte shadowSharpness;
    
}