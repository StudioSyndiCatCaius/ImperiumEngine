namespace ImperiumCore.Enums;

// How a 2D node arranges its children. Free = children self-position via anchors/offsets;
// Vertical/Horizontal = box layout managed by this node.
public enum ELayoutMode
{
    Free,
    Vertical,
    Horizontal,
}

// Whether a node participates in mouse hit-testing.
public enum EMouseFilter
{
    Stop,   // receives mouse and blocks nodes behind it
    Pass,   // passes through to whatever is behind, children still tested
    Ignore, // never a mouse target
}

[Flags]
public enum ESizeFlags
{
    None = 0,
    Fill = 1,
    Expand = 2,
    ShrinkCenter = 4,
    ShrinkEnd = 8,
    ExpandFill = Expand | Fill,
}

public enum EAnchorPreset
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    CenterLeft,
    CenterTop,
    CenterRight,
    CenterBottom,
    Center,
    LeftWide,
    TopWide,
    RightWide,
    BottomWide,
    VCenterWide,
    HCenterWide,
    FullRect,
}
