using ImperiumCore.Classes;
using ImperiumEditor.Assets;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Scenes;

public class EditorLevel_Projects : ImpLevel
{
    
    public O2D_TabContainer ui_MainTabs;

    // -------------------------------------------------------------------------------------------
    // TAB -- Projects
    // -------------------------------------------------------------------------------------------
    public ImpComponent2D uiTab_projects; // tab to list all projects
    public O2D_List ui_Projects = new O2D_List();
    public List<EdA_Project> Projects;
    
    public O2D_Button btn_newProject;
    
    
    private void Project_OnSelected(O2D_List _list, ImpComponent2D _proj, int _index)
    {
        //when project selected, should open
    }
    
    // -------------------------------------------------------------------------------------------
    // TAB -- Config
    // -------------------------------------------------------------------------------------------
    
    public ImpComponent2D uiTab_config; // tab to configure the engine, things like extra project paths, or autoload last project
    
    
    // -------------------------------------------------------------------------------------------
    // -------------------------------------------------------------------------------------------
    // IMPLEMENT
    // -------------------------------------------------------------------------------------------
    // -------------------------------------------------------------------------------------------

    public EditorLevel_Projects()
    {
        ui_Projects.OnItemSelect = Project_OnSelected;
        
        //load all projects and add to list
        foreach (var p in Projects)
        {
            //add to list
            O2D_ED_Project proj = new O2D_ED_Project(p);
            ui_Projects.Child_Add(proj);
        }
    }
}

public class O2D_ED_Project : O2D_ListItem
{
    EdA_Project _project;
    
    public O2D_Button button = new O2D_Button();
    public O2D_List sublist = new O2D_List();
    public O2D_Text ui_text = new O2D_Text();
    
    public O2D_ED_Project(EdA_Project project)
    {
        _project = project;
        SourceObject=_project;
        Child_Add(button);
        Child_Add(sublist);
        sublist.Child_Add(ui_text);
        ui_text.text = _project.Title;
    }
}