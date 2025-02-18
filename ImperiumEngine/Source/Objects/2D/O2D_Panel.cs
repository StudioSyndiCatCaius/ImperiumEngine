using System.Numerics;
using ImGuiNET;

namespace ImperiumEngine.Source.Objects._2D;

public class O2D_Panel : ImpComponent2D
{
    public override void On_Draw(double delta)
    {
        ImGui.Begin(GUID.ToString());
        //ImGui.SetWindowSize(GUID.ToString(),new Vector2(300,200));
        ImGui.End();
        base.On_Draw(delta);
    }
}