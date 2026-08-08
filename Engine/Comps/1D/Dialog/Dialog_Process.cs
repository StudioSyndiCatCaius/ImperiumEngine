using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;


public class Dialog_Process : C1_Dialog
{
    [ImpVar] public string text;
    [ImpVar] public bool can_cancel=true;
    
    public C2_Progresser? progresser;
    
    public Action on_cancel;
    public Action on_complete;
}