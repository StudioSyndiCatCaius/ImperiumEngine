using ImGuiNET;

namespace Editor;

public class EdPanel : EdUi
{
    public string title = "Panel";
    public uint dock_id;

    public override void OnDraw()
    {
        if (dock_id != 0)
            ImGui.SetNextWindowDockID(dock_id, ImGuiCond.Always);

        if (!ImGui.Begin(title))
        {
            ImGui.End();
            return;
        }
        OnDrawPanel();
        ImGui.End();
    }

    public virtual void OnDrawPanel() { }
}
