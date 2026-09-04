using System.Numerics;
using ImGuiNET;

namespace Editor.Windows;

public class WND_Scene : EdWindow
{
    public override void Draw()
    {
        base.Draw();
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
        ImGui.Text("Right pane");
        ImGui.EndChild();
            
        ImGui.BeginChild("targo", new Vector2(0,h), true, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.Text("Right pane");
        ImGui.EndChild();
            
        ImGui.EndChild();


    }
}