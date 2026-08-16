namespace ImperiumEngine.Dialogs;

// a dialog that runs a process with a progress bar
public class Dialog_Process : ImpDialog
{
    public bool can_cancel=true;
    public bool is_running;
    public float progress;
    
    public Func<float> query_progress;
    
    public Action on_complete;
    public Action on_cancel;
}