using ImGuiNET;

namespace ImperiumEngine.Source.Nodes._2D;

public class N2D_Panel : ImpObject2D
{
    public override void OnBegin()
    {
        base.OnBegin();
    }

    public override void OnDraw(double delta)
    {
        ImGui.Begin(name);
        ImGui.End();
        
        base.OnDraw(delta);
    }
}