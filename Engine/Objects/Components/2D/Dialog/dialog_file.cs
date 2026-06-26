namespace ImperiumEngine.Objects._2D.Dialog;

//File Access Save/Load/Create dialog
public class O2D_Dialog_File : O2D_Dialog
{
    //for now relly on OS native save/load/file access dialogue
    public bool useOSNative = true;
    
    public Action<string> OnSelect;
}