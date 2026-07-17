using System.Diagnostics;
using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumEditor.Config;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Scenes;

public class EditorLevel_Projects : ImpLevel
{
    public O2D_TabContainer ui_MainTabs = new O2D_TabContainer();

    // -------------------------------------------------------------------------------------------
    // TAB -- Projects
    // -------------------------------------------------------------------------------------------
    public ImpComponent2D uiTab_projects;
    public O2D_List ui_Projects = new O2D_List();
    public List<A_Project> Projects = new();

    public O2D_Button btn_newProject;


    private void Project_OnSelected(O2D_List _list, ImpComponent2D _proj, int _index)
    {
        //when project selected, should open
    }

    // -------------------------------------------------------------------------------------------
    // TAB -- Config
    // -------------------------------------------------------------------------------------------

    public ImpComponent2D uiTab_config;
    public O2D_Inspector ui_Config_pEdit = new O2D_Inspector();


    // -------------------------------------------------------------------------------------------
    // -------------------------------------------------------------------------------------------
    // IMPLEMENT
    // -------------------------------------------------------------------------------------------
    // -------------------------------------------------------------------------------------------

    public EditorLevel_Projects()
    {
        ui_MainTabs.Anchors_Preset(EAnchorPreset.FullRect);
        ui_MainTabs.offset_left = 16;
        ui_MainTabs.offset_top = 16;
        ui_MainTabs.offset_right = -16;
        ui_MainTabs.offset_bottom = -16;

        // -----------------------------------------------
        // SETUP - Projects
        // -----------------------------------------------

        Projects = Projects_Dummy(10);

        ui_Projects.OnItemSelect = Project_OnSelected;
        ui_Projects.size_flags_v = ESizeFlags.ExpandFill;
        foreach (var p in Projects)
            ui_Projects.Child_Add(new O2D_ED_Project(p));

        var _title = new O2D_Text { text = "Projects" };
        _title.style.font_size = 28;

        btn_newProject = new O2D_Button { text = "New Project" };
        btn_newProject.custom_minimum_size = new Vector2(0, 40);
        btn_newProject.OnPressed = _ => Console.WriteLine("[Projects] new project");

        uiTab_projects = new ImpComponent2D { layout_mode = ELayoutMode.Vertical, separation = 10 };
        uiTab_projects.Anchors_Preset(EAnchorPreset.FullRect);
        uiTab_projects.offset_left = 16;
        uiTab_projects.offset_top = 16;
        uiTab_projects.offset_right = -16;
        uiTab_projects.offset_bottom = -16;
        uiTab_projects.Child_Add(_title);
        uiTab_projects.Child_Add(ui_Projects);
        uiTab_projects.Child_Add(btn_newProject);

        // -----------------------------------------------
        // SETUP - Config
        // -----------------------------------------------
        var _cfgLaunch = ImpConfig.Get<CFG_ED_Launch>();
        ui_Config_pEdit.Objects_Set(_cfgLaunch);

        O2D_List ui_Config_MainVList = new() { layout_mode = ELayoutMode.Vertical };

        O2D_List ui_Config_BtnList = new() { layout_mode = ELayoutMode.Horizontal };

        O2D_Button btn_OpenConfigPath = new()
        {
            text = "Open Config Path",
            OnPressed = _ =>
            {
                var dir = _cfgLaunch.Config_GetSaveDir();
                if (Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
        };
        
        
        uiTab_config = new ImpComponent2D { layout_mode = ELayoutMode.Vertical };
        uiTab_config.Anchors_Preset(EAnchorPreset.FullRect);
        
        ui_Config_BtnList.Child_Add(btn_OpenConfigPath);
        
        uiTab_config.Child_Add(ui_Config_MainVList);
        ui_Config_MainVList.Child_Add(ui_Config_BtnList);
        ui_Config_MainVList.Child_Add(ui_Config_pEdit);

        // -----------------------------------------------
        // Finalize
        // -----------------------------------------------

        ui_MainTabs.Tab_Add(uiTab_projects, "Projects");
        ui_MainTabs.Tab_Add(uiTab_config, "Config");

        Object_Add(ui_MainTabs);
    }

    private static List<A_Project> Projects_Dummy(int count)
    {
        var _list = new List<A_Project>();
        for (int i = 1; i <= count; i++)
            _list.Add(new A_Project { Title = $"Project {i}" });
        return _list;
    }
}

public class O2D_ED_Project : O2D_ListItem
{
    readonly A_Project _project;

    public O2D_Button button = new O2D_Button();

    public O2D_ED_Project(A_Project project)
    {
        _project = project;
        SourceObject = _project;

        layout_mode = ELayoutMode.Vertical;
        custom_minimum_size = new Vector2(0, 36);

        button.text = project.Title;
        button.size_flags_v = ESizeFlags.ExpandFill;
        button.OnPressed = _ => Console.WriteLine($"[Projects] pressed '{project.Title}'");
        Child_Add(button);
    }

    protected  override void OnBegin()
    {
        button.icon = ImpAsset.Load<A_Texture2D>("T_ico_barsHorizontal");
    }
}
