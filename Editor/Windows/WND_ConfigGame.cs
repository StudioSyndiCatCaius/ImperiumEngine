using System.Numerics;
using Editor.Panels;
using Editor.UI;
using Engine.Globals;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Windows;

/*
 *  Game config: categorizes and displays all `[ImpVar][Config] static` vars, written them to `Game.Toml` in the projects "Config" folder
 * left: list of all gathered classes
 * right: inspector for currently selected Class category
 */
public class WND_ConfigGame : EdWindow
{
    public PNL_Inspector inspector = new();
    [EdConfig] public float left_panel_width = 240f;

    readonly EUI_SearchBar search = new() { hint = "Search classes" };
    Type? selected;

    public WND_ConfigGame()
    {
        inspector.static_config = true;
        inspector.on_changed = GConfig.SaveGame;
    }

    public override void OnDraw()
    {
        base.OnDraw();

        List<Type> classes = GConfig.Gather();
        if (selected == null && classes.Count > 0)
            selected = classes[0];

        float avail_x = ImGui.GetContentRegionAvail().X;
        float splitter = 6f;
        left_panel_width = Math.Clamp(left_panel_width, 140f, MathF.Max(140f, avail_x - 180f));

        ImGui.BeginChild("##cfg_classes", new Vector2(left_panel_width, 0), true);
        search.OnDraw();
        ImGui.Separator();
        ImGui.BeginChild("##cfg_class_list", Vector2.Zero, false);
        string q = search.search_text ?? "";
        bool filtering = !string.IsNullOrWhiteSpace(q);
        for (int i = 0; i < classes.Count; i++)
        {
            Type t = classes[i];
            string title = GConfig.Title(t);
            if (filtering
                && !title.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(t.FullName ?? t.Name);
            Texture2D ico = PNL_Inspector.TypeIcon(t);
            if (ico.Id != 0)
            {
                float sz = ImGui.GetTextLineHeight();
                ImGui.Image((IntPtr)ico.Id, new Vector2(sz, sz));
                ImGui.SameLine(0, 4);
            }
            if (ImGui.Selectable(title, t == selected))
                selected = t;
            ImGui.PopID();
        }
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, 0);
        ImGui.InvisibleButton("##cfg_split", new Vector2(splitter, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive())
            left_panel_width += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);

        ImGui.SameLine(0, 0);
        ImGui.BeginChild("##cfg_inspector", Vector2.Zero, true);
        inspector.inspect_type = selected;
        inspector.selected_object = null;
        inspector.OnDrawPanel();
        ImGui.EndChild();
    }
}
