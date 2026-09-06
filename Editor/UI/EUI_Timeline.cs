namespace Editor.UI;

public class EUI_Timeline //base timeline class. (will probably need something for saving and restoring an objects vars when done editing it with the track.)
{
    public bool is_editable;
    
    public bool is_playing;
    public float time;
    public EUI_TimelineTrack master_track;
}


public class EUI_TimelineTrack
{
    public bool is_editable;
    public Type type;
    public string source_target; // the thing this timeline tracks is reffering to
    
    public EUI_TimelineTrack[] subtracks; //such as when editing specific vars on an ImpComp
    public EUI_TimelineTrack? parent;
}

