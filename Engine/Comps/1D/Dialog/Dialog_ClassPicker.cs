using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_ClassPicker : C1_Dialog
{
    public C2_Box box=new ();
    public C2_Tree class_tree=new ();
    public C2_Text class_name=new ();
    
    public C2_TextEdit txtedit_create_name=new ();
    
    public bool is_create_new = false; // if true, shows the bar to edit the new class name
    
    public C2_Button btn_confirm=new ();
    public C2_Button btn_cancel=new ();
    
    public  Type root_type;
    public  Type selected_type;
    
    public Action<Dialog_ClassPicker> on_confirm;
    public Action<Dialog_ClassPicker> on_cancel;
    
    public Dialog_ClassPicker()
    {
        
    }
    
    // sets the root type of the tree and rebuilds it
    public void BuildRootType(Type type, Action<Type> filter=null)
    {
        root_type = type;
    }
    
    public void BuildTree()
    {
        
    }
}