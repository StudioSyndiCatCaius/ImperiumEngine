using ImperiumEngine.Objects._2D;

namespace ImperiumEditor;

public class EditorWindow : O2D_Window
{
    public int maxNum = 1; // maxmimum instances of this window allowed. 0 = unlimited
    
}

// =========================================================================================================
// //Settings Window
// =========================================================================================================

public class SettingsWindow : EditorWindow
{
    public O2D_List ui_categories = new O2D_List(); 
    public O2D_Inspector ui_inspector = new O2D_Inspector();


    public SettingsWindow()
    {
        ui_categories.OnItemSelect = (list, comp, i) =>
        {
            if (comp is O2D_ListItem item)
            {
                ui_inspector.Objects_Set(item.SourceObject);
            }
        };

    }
}
