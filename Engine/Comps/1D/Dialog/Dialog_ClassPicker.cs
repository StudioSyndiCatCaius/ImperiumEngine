using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;


// displays a tree list of all child classes from a given object
public class Dialog_ClassPicker : C1_Dialog
{
    [ImpVar] public string title;
    [ImpVar] public string description;
    
    [ImpVar] public Type[] types;
    [ImpVar] public Type[] exclude = [];
    [ImpVar] public int allowed_depth=-1; //if >=0, only shows classes at this depth or below. 0= only show top level. 
    
    public Type? selected;
    
    public C2_Tree class_tree;
    public C2_Button btn_ok;
    public C2_Button btn_cancel;
    
    public Action<Type>? on_confirm;
    public Action? on_cancel;
}