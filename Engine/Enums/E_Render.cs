namespace ImperiumEngine.Enums;

[Flags]
public enum EDrawFlags
{
    NONE            = 0,
    EDITOR_DEBUG    = 1 << 0, //if editor has debug NOT runtime mode turned on (Toggled with 'G'. This mode shows things like labels & bounding boxes
    EDITOR_SELECTED = 1 << 1, //if this object is selected in the editor
    DEBUG_PASS      = 1 << 2, //set on the second OnDraw call each frame, inside raylib's BeginMode3D (after R3D.End). R3D mesh submission only works when NOT set; raylib shapes/billboards only work when set
}