namespace ImperiumEngine.Comps._1D.Dialog;


public enum EFileMode
{
    Read, Write
}

public class Dialog_File : C1_Dialog
{
    [ImpVar] public EFileMode mode;
    [ImpVar] public string path;
    [ImpVar] public bool allow_ascend=true; // if true, allows moving up from the folder "path" is when dialog opened. 
    [ImpVar] public bool allow_multiple=false;
    [ImpVar] public bool folder_select=false; // if true, path is a folder, not a file.
    [ImpVar] public List<string> filter;
    
    
    Action<string> on_select;
}