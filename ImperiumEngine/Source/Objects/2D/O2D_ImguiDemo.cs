using ImGuiNET;

namespace ImperiumEngine.Source.Objects._2D;

public class O2D_ImguiDemo : ImpComponent2D
{
    public override void On_Draw(double delta)
    {
        ImGui.ShowDemoWindow();
        base.On_Draw(delta);
    }
}