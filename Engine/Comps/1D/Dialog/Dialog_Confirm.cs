using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_Confirm : C1_Dialog
{
    public string message;

    public string text_yes = "Yes";
    public string text_no = "No";
    
    public Action on_yes;
    public Action on_no;
    
    C2_Box box=new ();
    C2_Text text=new ();
    C2_Button btn_yes=new ();
    C2_Button btn_no=new ();

    public async Task Run(string _message, Action _on_yes, Action _on_no)
    {
        box.is_visible=true;
        
        //wait for the user to press a button.
        
        box.is_visible=false;
    }
}