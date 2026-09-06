using System.Numerics;
using Engine.Globals;
using ImGuiNET;

namespace Editor.Panels;

public class PNL_Log : EdPanel
{
    bool _stick = true;

    public PNL_Log()
    {
        title = "Log";
    }

    public override void OnDrawPanel()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Log");
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear"))
            GLog.Clear();
        ImGui.SameLine();
        ImGui.TextDisabled(GLog.entries.Count + " entries");

        ImGui.BeginChild("##log_lines", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        for (int i = 0; i < GLog.entries.Count; i++)
        {
            TLogEntry e = GLog.entries[i];
            Vector4 col = new(e.color.R / 255f, e.color.G / 255f, e.color.B / 255f, e.color.A / 255f);
            ImGui.PushStyleColor(ImGuiCol.Text, col);
            ImGui.TextUnformatted(e.text);
            ImGui.PopStyleColor();
        }
        if (_stick)
            ImGui.SetScrollHereY(1f);
        _stick = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f;
        ImGui.EndChild();
    }
}