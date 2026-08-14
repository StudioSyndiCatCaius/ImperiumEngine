using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_Alert : C1_Dialog
{
    public string message;
    public Action on_ok;
    
    C2_Box box=new ();
    C2_Text text=new ();
    C2_Button btn_ok=new ();

    public async Task Run(string _message, Action _on_ok)
    {
        box.is_visible=true;
        
        //wait for the user to press a button.
        
        box.is_visible=false;
    }
}