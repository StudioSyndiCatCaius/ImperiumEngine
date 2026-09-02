using System.Numerics;
using Editor.Windows;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Core;
using ImGuiNET;


namespace Editor.Scenes;



public class SCN_Editor : A_Scene
{
    public SCN_Editor()
    {
        root = new SNC_Editor_Root();
    }
}


public class SNC_Editor_Root : ImpComp
{
    public WND_Scene wnd_scene = new();
    public WND_Assets wnd_assets = new();
    public WND_ConfigGame wnd_config_game = new();
    public WND_ConfigEditor wnd_config_editor = new();

    private EdWindow active_window;
    
    public void Draw_MainWindow(EdWindow w, string nam)
    {
        if (!ImGui.BeginTabItem(nam)) return;
        active_window = w;
        w.Draw();
        ImGui.EndTabItem();
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);

        ImGuiViewportPtr vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(vp.Size);
        ImGuiWindowFlags host_flags =
            ImGuiWindowFlags.NoTitleBar | 
            ImGuiWindowFlags.NoCollapse | 
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoBringToFrontOnFocus | 
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.MenuBar;
        ImGui.Begin("##editor", host_flags);
        // ------------------------------------------------------------------------------------------------------------
        // Menu Bar
        // ------------------------------------------------------------------------------------------------------------
        ImGui.BeginMainMenuBar();
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New Scene"))
            {
                
            }
            if (ImGui.MenuItem("New Asset"))
            {
                
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", "Ctrl+Z", false, false))
            {
                
            }
            if (ImGui.MenuItem("Redo", "Ctrl+Y / Ctrl+Shift+Z", false, false))
            {
                
            }
            ImGui.Separator();
            ImGui.EndMenu();
            
        }
        
        ImGui.EndMainMenuBar();
        // ------------------------------------------------------------------------------------------------------------
        // Main Buttons
        // ------------------------------------------------------------------------------------------------------------
        ImGui.Separator();

        if (ImGui.Button("New Scene"))
        {
            
        }
        ImGui.SameLine();
        if (ImGui.Button("New Asset"))
        {
            
        }
        ImGui.SameLine();
        if (ImGui.Button("Play"))
        {
            
        }
        ImGui.SameLine();
        if (ImGui.Button("Play  (From Start)"))
        {
            
        }
        
        // ------------------------------------------------------------------------------------------------------------
        // Main Tabs
        // ------------------------------------------------------------------------------------------------------------
        ImGui.Separator();
        ImGui.BeginTabBar("##tabs");
            Draw_MainWindow(wnd_scene, "Scene");
            Draw_MainWindow(wnd_assets, "Assets");
            Draw_MainWindow(wnd_config_game, "Game Config");
            Draw_MainWindow(wnd_config_editor, "Editor Config");
        ImGui.EndTabBar();
        // ------------------------------------------------------------------------------------------------------------
        // 
        // ------------------------------------------------------------------------------------------------------------

        // ------------------------------------------------------------------------------------------------------------
        ImGui.End(); // Editor End
    }

    public override void OnDraw3D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw3D(dt, flags);
    }
}