namespace ImperiumEngine.Enums;


public enum EUIAlignment
{
    Vertical, Horizontal,
}


public enum EUIAnchorPreset
{
    Full,
    TopLeft, TopRight, BottomLeft, BottomRight,
    Center, CenterLeft, CenterRight, CenterTop, CenterBottom,
    WideLeft, WideRight, WideTop, WideBottom, 
    WideCenterH, WideCenterV
}

public enum EUISizingPreset
{
    Fill, ShrinkBegin, ShrinkCenter, ShrinkEnd,
}

public enum ECursorFilter
{
    Hit, //this ui can get cursor focus
    Pass, //this ui cannot get cursor focus but its children can (depending on their filter)
    Ignore, //this ui cannot get cursor focus nor its children
}

public enum ECursorEvent
{
    Pressed, //mouse went down on this comp
    Released, //mouse came up over this comp, regardless of where it went down
    Clicked, //mouse went down AND came up on this comp
    Wheel, //wheel scrolled while hovering this comp
}