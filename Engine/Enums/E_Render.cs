namespace ImperiumEngine.Enums;

[Flags]
public enum EDrawFlags
{
    EDITOR_DEBUG, //if editor has debug NOT runtime mode turned on (Toggled with 'G'. This mode shows things like labels & bounding boxes
    EDITOR_SELECTED, //if this object is selected in the editor
}