using System.Numerics;
using Editor.Dialog;
using Editor.Panels;
using Editor.UI;
using Engine.Assets;
using Engine.Comps._2D;
using ImGuiNET;


namespace Editor.Windows;

public class WND_Scene : EdWindow
{
    public PNL_Inspector inspector_comp = new();
    public PNL_Inspector inspector_scene = new();
    public PNL_SceneTree scene_tree = new();

    public List<EUI_SceneTab> scene_tabs = new();
    public EUI_SceneTab? current_scene_tab = null;
    
    public override void OnDraw()
    {
        base.OnDraw();
        float leftWidth = ImGui.GetContentRegionAvail().X-300;
        
        // Left pane — drag its right edge
        ImGui.BeginChild("left", new Vector2(leftWidth, 0), true, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.Text("Left pane");
        ImGui.EndChild();


        ImGui.SameLine();

        ImGui.BeginChild("right", new Vector2(0, 0), true,ImGuiWindowFlags.AlwaysAutoResize);
        
        float availY = ImGui.GetContentRegionAvail().Y;
        float gap = ImGui.GetStyle().ItemSpacing.Y;
        float h = MathF.Max(0f, (availY - gap) * 0.5f);

        
        ImGui.BeginChild("targa", new Vector2(0, h), true, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.BeginTabBar("##comptabs");
        {
            if(ImGui.BeginTabItem("Outliner"))
            {
                scene_tree.OnDraw();
                ImGui.EndTabItem();
            }
            
            if(ImGui.BeginTabItem("Common")) //drwas a list of common comps that can be dragged & dropped onto the current scene
            {
                
                ImGui.EndTabItem();
            }
        }
        ImGui.EndTabBar();
        ImGui.EndChild();
            
        ImGui.BeginChild("targo", new Vector2(0,h), true, ImGuiWindowFlags.AlwaysAutoResize);
        
        ImGui.BeginTabBar("##inspectortabs");
        {
            if(ImGui.BeginTabItem("Comp"))
            {
                inspector_comp.OnDraw();
                ImGui.EndTabItem();
            }
            
            if(ImGui.BeginTabItem("Scene"))
            {
                inspector_scene.OnDraw();
                ImGui.EndTabItem();
            }
        }
        ImGui.EndTabBar();
        ImGui.EndChild();
            
        ImGui.EndChild();


    }
    
    // ======================================================================================
    // Scene
    // ======================================================================================
    
    public void Scene_Open(A_Scene scene)
    {
        foreach (EUI_SceneTab tab in scene_tabs)
        {
            if (tab.scene == scene)
            {
                current_scene_tab = tab;
                return;
            }
        }
        current_scene_tab = new EUI_SceneTab();
        scene_tabs.Add(current_scene_tab);
    }
    
    public void Scene_Close(A_Scene scene)
    {
        if (scene.is_dity)
        {
            EDLG_Confirm.Run("Scene is unsaved. Delete anyway?", (b) =>
            {
                if (b) Scene_CloseConfirm(scene);
            });
        }
        else Scene_CloseConfirm(scene);
    }

    private void Scene_CloseConfirm(A_Scene scene)
    {
        foreach (EUI_SceneTab tab in scene_tabs)
        {
            if (tab.scene == scene)
            {
                scene_tabs.Remove(tab);
                return;
            }
        }
    }
}

// ####################################################################################################################
// Scene Tab
// ####################################################################################################################

public enum EEditorScene_View { View_3D, View_2D }
public enum EEditorGizmo_Mode { Translate, Rotate, Scale }
public enum EEditorGizmo_Orientation { Local, World }

public class EUI_SceneTab : EdUi
{
    public A_Scene? scene;
    
    public C2_Viewport3D viewport3d;
    public C2_Viewport2D viewport2d;

    public EUI_EnumToggle enumtoggle_view = new();
    public EUI_EnumToggle enumtoggle_gizmo_mode = new();
    public EUI_EnumToggle enumtoggle_gizmo_orientation = new();
    
    public EEditorScene_View view = EEditorScene_View.View_3D;
    public EEditorGizmo_Mode gizmo_mode = EEditorGizmo_Mode.Translate;
    public EEditorGizmo_Orientation gizmo_orientation = EEditorGizmo_Orientation.Local;

    public override void OnDraw()
    {
        base.OnDraw();
    }
}