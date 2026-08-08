using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._2D;

//display children as an array list of widgets on some custom layout manner
public class C2_List : ImpComponent2D
{
    [ImpVar] public List2DStyle style;
    //normalized curve for layout out the 2d children. x & y for position, z for rotation
    [ImpVar] public A_Curve_Vector layout_curve;
}


public class List2DStyle
{
    
}

// ===================================================================================
// styles
// ===================================================================================



public class List2DStyle_Simple : List2DStyle
{
    public ELayoutMethod layout;
    public bool fill;
}

public class List2DStyle_ScrollBox : List2DStyle
{
    public ELayoutMethod layout;
}