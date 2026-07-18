namespace ImperiumEngine.Enums;

[Flags]
public enum EDrawFlags
{
    NONE            = 0,
    EDITOR_DEBUG    = 1 << 0, //if editor has debug NOT runtime mode turned on (Toggled with 'G'. This mode shows things like labels & bounding boxes
    EDITOR_SELECTED = 1 << 1, //if this object is selected in the editor
}