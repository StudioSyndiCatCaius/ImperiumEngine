using ImGuiNET;

namespace ImperiumEngine.Source.Nodes._2D;

public class N2D_MenuBar : ImpObject2D
{
    public override void OnDraw(double delta)
    {
        ImGui.BeginMainMenuBar();
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New Project", "")) 
            {
                // Handle New action
            }
            if (ImGui.MenuItem("Open Project", "")) 
            {
                // Handle New action
            }
            ImGui.Separator();
            if (ImGui.MenuItem("New Scene", "Ctrl+N")) 
            {
                // Handle New action
            }
            if (ImGui.MenuItem("Open", "Ctrl+O"))
            {
                // Handle Open action
            }
            if (ImGui.MenuItem("Save Scene", "Ctrl+S"))
            {
                // Handle Save action
            }
            if (ImGui.MenuItem("Save Scene As", "Ctrl+Shift+S"))
            {
                // Handle Save action
            }
            
            ImGui.Separator();
            if (ImGui.MenuItem("Exit", "Alt+F4"))
            {
                // Handle Exit action
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", "Ctrl+Z"))
            {
                // Handle Undo action
            }
            if (ImGui.MenuItem("Redo", "Ctrl+Y"))
            {
                // Handle Redo action
            }
            ImGui.Separator();
            if (ImGui.MenuItem("Cut", "Ctrl+X"))
            {
                // Handle Cut action
            }
            if (ImGui.MenuItem("Copy", "Ctrl+C"))
            {
                // Handle Copy action
            }
            if (ImGui.MenuItem("Paste", "Ctrl+V"))
            {
                // Handle Paste action
            }
            ImGui.EndMenu();
        }
        ImGui.EndMainMenuBar();
        
        base.OnDraw(delta);
    }
}