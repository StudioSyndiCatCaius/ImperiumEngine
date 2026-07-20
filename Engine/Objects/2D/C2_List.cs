using ImperiumEngine.Classes;
using ImperiumEngine.Enums;

namespace ImperiumEngine.Objects._2D;

//display children as an array list of widgets on some custom layout manner
public class C2_List : ImpComponent2D
{
    public List2DStyle style;
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