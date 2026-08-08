using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Objects._2D;

public class C2_Text : ImpComponent2D
{
    [ImpVar] public TText text;
    [ImpVar] public A_TextStyle style;

    //when text is changed, it will not all be rendered at once, but built across the screen over time. this does NOT change the screen bounds size of the text box
    [ImpVar] public bool build_text;
    //time beteween each added line in a text build
    [ImpVar] public bool build_text_tick;
    readonly bool is_building;

    public void BuildText_Start()
    {
        
    }

    public void BuildText_Stop(bool JumpToEnd = true)
    {
        
    }
    
    Action On_BuildText_Begin;
    Action On_BuildText_Finish;
}

public class A_TextStyle : ImpAsset
{
    [ImpVar] public float font_size=10;
    [ImpVar] public Color font_color=Color.White;
    
    [ImpVar] public float outline_size;
    [ImpVar] public Color outline_color;
    
    [ImpVar] public Vector2 shadow_offset;
    [ImpVar] public Color shadow_color=new Color(0,0,0,0.0f);
}