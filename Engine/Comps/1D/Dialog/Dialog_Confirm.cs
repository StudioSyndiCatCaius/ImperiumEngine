using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_Confirm : C1_Dialog
{
    [ImpVar] public string text="Do this thing?";
    [ImpVar] public string text_yes="Yes";
    [ImpVar] public string text_no="No";
    
    public Action<bool> on_confirm;

    public C2_Button? button_yes = new();
    public C2_Button? button_no = new ();

    public Dialog_Confirm()
    {
        button_yes.text = text_yes;
        button_no.text = text_no;
    }
    
}