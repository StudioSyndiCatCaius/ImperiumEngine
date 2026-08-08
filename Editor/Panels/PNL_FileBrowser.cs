using ImperiumEngine.Comps._1D.Dialog;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Main;

namespace Editor.Panels;

public class PNL_FileBrowser : EdPanel
{
    // --------------------------------------------
    // UI
    // --------------------------------------------
    public C2_Tree file_tree_game = new C2_Tree();
    public C2_Tree file_tree_editor = new C2_Tree();
    
    public C2_TabBox tree_tabs = new C2_TabBox();

    public C2_List list_filepath = new C2_List();
    public C2_List list_files = new C2_List();
    
    // --------------------------------------------
    // Dialog
    // --------------------------------------------
    public Dialog_Confirm confirm_delete;
    public Dialog_Confirm confirm_move;
    
    public Dialog_Process process_import;
    public Dialog_Process process_delete;
    public Dialog_Process process_move; //used for both move & rename

    public Dialog_ClassPicker cpicker_new_asset; //creating a new .ImpAsset file
    public Dialog_ClassPicker cpicker_new_scene; //creating a new .ImpScene file
    public Dialog_ClassPicker cpicker_add_to_tree; //adding a new component to the scene tree
    
    // --------------------------------------------
    // misc
    // --------------------------------------------
    
    public EContentDir current_content_dir=EContentDir.Game;
    
    public PNL_FileBrowser()
    {
        name = "File Browser";

        C2_List _main = new();
        
        C2_List _list_r = new();
        
        Child_Add(_main);
        _main.Child_Add(tree_tabs);
        _main.Child_Add(_list_r);
        
        tree_tabs.Child_Add(file_tree_game);
        tree_tabs.Child_Add(file_tree_editor);
        
        _list_r.Child_Add(list_filepath);
        _list_r.Child_Add(list_files);
    }
}