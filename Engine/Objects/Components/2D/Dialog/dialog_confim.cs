namespace ImperiumEngine.Objects._2D.Dialog;

public class O2D_Dialog_Confirm : O2D_Dialog
{
    public string text;
    public string text_yes="Yes";
    public string text_no="No";
    
    public Action<bool> OnSelect;
}