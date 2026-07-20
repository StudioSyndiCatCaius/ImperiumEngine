using System.Numerics;
using Editor.Panels;
using ImGuiNET;
using ImperiumEngine.Objects.Assets;

namespace Editor.Windows;

public enum EPlayInEditorType
{
    NORMAL,     //normal game play mode. creat and posses new pawn and run level's GameMode states
    SIMULATE, //same as normal, BUT instead of possession of pawn, we simply keep the camera at the current editor camera position, so we can see a smiulation
}


public class WND_LevelEdit : EditorWindow
{
    public PNL_ComponentTree pnl_level_hierarchy = new();
    public PNL_Inspector pnl_object_inspector = new(); // inspector for selected entities/components properties
    public PNL_Inspector pnl_level_inspector = new(); // inspector for the level properties
    public PNL_World pnl_world = new(); // the 3d world

    public A_Level? level;

    float _side_width = 340f; //width of the right-hand column
    float _side_split = 0.45f; //outliner fraction of the right column height

    // exposed so editor session state (EditorConfig) can persist/restore the layout
    public float SideWidth { get => _side_width; set => _side_width = value; }
    public float SideSplit { get => _side_split; set => _side_split = value; }

    // tab shows the open level's file name, e.g. "Level: test"; unsaved shows a leading "*"
    public override string Title => $"Level: {LevelName}";
    // fixed docking id so retitling the tab (on level change) never resets the layout
    public override string WindowId => "Level Editor";
    public override bool CanClose => false; //the LevelEdit window is always open

    // the level is this window's document — Save hotkeys target it and the tab shows its dirty "*"
    public override ImperiumEngine.Classes.ImpAsset? DocumentAsset => level;

    string LevelName => level is { IsReference: true }
        ? Path.GetFileNameWithoutExtension(level.file_link)
        : "Untitled";

    public WND_LevelEdit()
    {
        panels = [pnl_world, pnl_level_hierarchy, pnl_object_inspector, pnl_level_inspector];

        // outliner and viewport share one selection list; both report changes here
        pnl_level_hierarchy.selection = pnl_world.selection;
        pnl_level_hierarchy.on_selection_changed = OnSelectionChanged;
        pnl_world.on_selection_changed = OnSelectionChanged;

        // any edit (inspector fields, gizmo drags, hierarchy restructures) dirties the level
        pnl_object_inspector.on_changed = MarkDirty;
        pnl_level_inspector.on_changed = MarkDirty;
        pnl_world.on_edited = MarkDirty;
        pnl_level_hierarchy.on_edited = MarkDirty;
    }

    void MarkDirty()
    {
        if (level != null) level.is_dirty = true;
    }

    void OnSelectionChanged()
    {
        var sel = pnl_world.selection;
        pnl_object_inspector.selected_objects = sel.Cast<object>().ToList();
        // gizmo edits all selected 3d entities, pivoting on their centroid
        pnl_world.gizmo.targets = sel.OfType<ImperiumEngine.Classes.ImpComponent3D>().ToList();
    }

    public void SetLevel(A_Level lvl)
    {
        level = lvl;
        pnl_world.level = lvl;
        pnl_level_hierarchy.components = lvl.components;
        pnl_level_inspector.selected_objects = [lvl];
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        DrawPlayToolbar();

        var avail = ImGui.GetContentRegionAvail();
        _side_width = Math.Clamp(_side_width, 150f, Math.Max(150f, avail.X - 200f));

        float splitter_w = 4f;
        float world_w = avail.X - _side_width - splitter_w - ImGui.GetStyle().ItemSpacing.X * 2;

        // --- world view (left, fills remaining space) ---
        ImGui.BeginChild("world_view", new Vector2(world_w, 0));
        pnl_world.Draw(delta);
        ImGui.EndChild();

        ImGui.SameLine();
        SplitterVertical("side_splitter", splitter_w, ref _side_width);
        ImGui.SameLine();

        // --- right column: outliner stacked on inspector ---
        ImGui.BeginChild("side_column", new Vector2(0, 0));
        {
            float h = ImGui.GetContentRegionAvail().Y;
            float top_h = Math.Clamp(h * _side_split, 60f, Math.Max(60f, h - 60f));

            ImGui.BeginChild("outliner", new Vector2(0, top_h), ImGuiChildFlags.Borders);
            ImGui.SeparatorText("Outliner");
            pnl_level_hierarchy.Draw(delta);
            ImGui.EndChild();

            SplitterHorizontal("side_hsplitter", 4f, ref _side_split, h);

            ImGui.BeginChild("inspector", new Vector2(0, 0), ImGuiChildFlags.Borders);
            if (ImGui.BeginTabBar("inspector_tabs"))
            {
                if (ImGui.BeginTabItem("Entity"))
                {
                    pnl_object_inspector.Draw(delta);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Level"))
                {
                    pnl_level_inspector.Draw(delta);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();
    }

    //top-right toolbar: green Play / Simulate buttons that start a Play-In-Editor session
    void DrawPlayToolbar()
    {
        float button_w = 90f;
        float total = button_w * 2 + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - total));

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.60f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.72f, 0.26f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.16f, 0.50f, 0.16f, 1f));

        if (ImGui.Button("Play", new Vector2(button_w, 0)))
            PlayInEditor(EPlayInEditorType.NORMAL);
        ImGui.SameLine();
        if (ImGui.Button("Simulate", new Vector2(button_w, 0)))
            PlayInEditor(EPlayInEditorType.SIMULATE);

        ImGui.PopStyleColor(3);
    }

    //starts a Play-In-Editor session in the given mode
    void PlayInEditor(EPlayInEditorType type)
    {
        //TODO: implement Play-In-Editor
    }

    //draggable vertical bar — dragging right shrinks the right column
    static void SplitterVertical(string id, float thickness, ref float side_width)
    {
        ImGui.InvisibleButton(id, new Vector2(thickness, -1));
        if (ImGui.IsItemActive())
            side_width -= ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
    }

    //draggable horizontal bar — adjusts the outliner/inspector split fraction
    static void SplitterHorizontal(string id, float thickness, ref float split, float total_height)
    {
        ImGui.InvisibleButton(id, new Vector2(-1, thickness));
        if (ImGui.IsItemActive() && total_height > 0)
            split = Math.Clamp(split + ImGui.GetIO().MouseDelta.Y / total_height, 0.1f, 0.9f);
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
    }
}
