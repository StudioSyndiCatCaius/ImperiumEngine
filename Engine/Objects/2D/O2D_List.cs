using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;

namespace ImperiumEngine.Objects._2D;

//lists and array of its child 2D objects

public abstract class ListStyle
{
}


public class O2D_List : ImpComponent2D
{
    public ListStyle style; // the default style is array

    public Action<O2D_List,ImpComponent2D,int> OnItemSelect;
    public Action<O2D_List,ImpComponent2D,int,bool> OnItemHover;
    public Action<O2D_List,ImpComponent2D,int,bool> OnItemHighlight;

    public O2D_List()
    {
        layout_mode = ELayoutMode.Vertical;
    }
}


// ############### LIST STYLES ########################################################################################

// ------------------------------------------------------------
// Array - most commonList type
// ------------------------------------------------------------

public class ListStyle_Array
{
    public ECommonLayout layout;
    
    public ECommonAlignment item_alignment_H;
    public ECommonAlignment item_alignment_V;
}

// ------------------------------------------------------------
// Scroll Box
// ------------------------------------------------------------

public class ListStyle_ScrollBox
{
    public ECommonLayout layout;
}