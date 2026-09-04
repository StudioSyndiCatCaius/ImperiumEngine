using Engine.Interfaces;

namespace Engine.Enums;


public enum EUIOrentation
{
    [Title("V")] V, 
    [Title("H")] H, 
}


public enum EUIPositionAlignment 
{
    Start, //Left, Top,
    Center, 
    End, //Right, Bottom
}



public enum ELayoutAlignment 
{
    Start, Center, End, Fill,
}



public enum ECursorFilter
{
    Pass, //this ui cannot get cursor focus but its children can (depending on their filter)
    Hit, //this ui can get cursor focus
    Block, //this ui cannot get cursor focus but its children cannot
    Ignore, //this ui cannot get cursor focus nor its children
}


// How C2_Text breaks lines when the available width is too narrow for the whole string.
public enum ETextWrap
{
    None,       // single line; overflow clips or spills depending on the draw path
    Word,       // break on spaces / punctuation (default)
    Arbitrary,  // break at any glyph once the line is full
}

public enum EImageLayout
{
    Stretch,
    Tile,
    NineSlice,
    Retain_Fit, //keeps the apect ratio of the image, making it always scale to fit the dimensions
    Retain_Fill, //keeps the apect ratio of the image, making it always scale to fill the dimensions
}
