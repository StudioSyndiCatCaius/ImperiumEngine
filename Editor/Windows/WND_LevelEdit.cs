using System.Numerics;
using Editor.Panels;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects._3D;
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

    // Play-In-Editor: a live, isolated deep copy of `level` that runs while playing. While it is
    // non-null the world/outliner show it instead of `level`, and edits (which are discarded on
    // stop) neither dirty the real document nor enter the undo history.
    public A_Level? play_level;
    EPlayInEditorType _play_type;
    ImpPlayer? _play_player;   // the live player for NORMAL play (null in SIMULATE)
    public bool IsPlaying => play_level != null;

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

    string LevelName => level is { is_reference: true }
        ? Path.GetFileNameWithoutExtension(level.file_link)
        : "Untitled";

    public WND_LevelEdit()
    {
        panels = [pnl_world, pnl_level_hierarchy, pnl_object_inspector, pnl_level_inspector];

        // outliner and viewport share one selection list; both report changes here
        pnl_level_hierarchy.selection = pnl_world.selection;
        pnl_level_hierarchy.on_selection_changed = OnSelectionChanged;
        pnl_world.on_selection_changed = OnSelectionChanged;

        // any edit (inspector fields, gizmo drags, hierarchy restructures) dirties the level.
        // Component OnInit re-runs when inspector edits *settle* (see PNL_Inspector.CommitEdits).
        pnl_object_inspector.on_changed = MarkDirty;
        pnl_level_inspector.on_changed = MarkDirty;
        pnl_world.on_edited = MarkDirty;
        pnl_level_hierarchy.on_edited = MarkDirty;

        // every panel records its edits into this window's undo history (Ctrl+Z / Ctrl+Y) —
        // except while playing, when edits target the throwaway play instance
        pnl_object_inspector.on_action = PushUndo;
        pnl_level_inspector.on_action = PushUndo;
        pnl_world.on_action = PushUndo;
        pnl_level_hierarchy.on_action = PushUndo;
    }

    // edits made while playing act on the discarded play instance, so they must not dirty the real
    // document nor pollute the undo history
    void MarkDirty()
    {
        if (!IsPlaying && level != null) level.is_dirty = true;
    }

    void PushUndo(IUndoable action)
    {
        if (!IsPlaying) history.Push(action);
    }

    // deletes the current selection (Delete hotkey). Selection is shared between the outliner and
    // the viewport, so this covers entities picked in either.
    public void DeleteSelection() => pnl_level_hierarchy.DeleteSelection();

    void OnSelectionChanged()
    {
        var sel = pnl_world.selection;
        // Descendants of a locked node never appear in the Entity inspector. The locked node
        // itself still inspects normally; its children are also hidden from the outliner tree.
        pnl_object_inspector.selected_objects = sel
            .Where(c => !c.HasLockedAncestor())
            .Cast<object>()
            .ToList();
        // gizmo follows the same set the inspector shows (no transforming hidden locked children)
        pnl_world.gizmo.targets = sel
            .OfType<ImperiumEngine.Classes.ImpComponent3D>()
            .Where(c => !c.HasLockedAncestor())
            .ToList();
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

    //top-right toolbar: green Play / Simulate buttons that start a Play-In-Editor session, or a
    //red Stop button while one is running
    void DrawPlayToolbar()
    {
        float button_w = 90f;
        int count = IsPlaying ? 1 : 2;
        float total = button_w * count + ImGui.GetStyle().ItemSpacing.X * (count - 1);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - total));

        if (IsPlaying)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.65f, 0.18f, 0.18f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.78f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.55f, 0.14f, 0.14f, 1f));

            // label reflects the running mode; Shift+Esc also stops (see Program.HandleStopPlayHotkey)
            string label = _play_type == EPlayInEditorType.SIMULATE ? "Stop Sim" : "Stop";
            if (ImGui.Button($"{label}###play_stop", new Vector2(button_w, 0)))
                StopPlayInEditor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stop Play-In-Editor (Shift+Esc)");

            ImGui.PopStyleColor(3);
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.60f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.72f, 0.26f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.16f, 0.50f, 0.16f, 1f));

        ImGui.BeginDisabled(level == null);
        if (ImGui.Button("Play", new Vector2(button_w, 0)))
            PlayInEditor(EPlayInEditorType.NORMAL);
        ImGui.SameLine();
        if (ImGui.Button("Simulate", new Vector2(button_w, 0)))
            PlayInEditor(EPlayInEditorType.SIMULATE);
        ImGui.EndDisabled();

        ImGui.PopStyleColor(3);
    }

    // Starts a Play-In-Editor session: clones the level into an isolated instance, tears down the
    // editor preview's live resources (so its R3D lights don't double up with the clone's), and
    // brings the clone to life. The world view and outliner switch to the running instance.
    //
    // NORMAL plays the level as the packaged game would: a live ImpPlayer drives input and the
    // level's active game camera renders the view (see PNL_World.RenderGame / Engine/Program.cs).
    // SIMULATE runs the same simulation but observed through the editor camera, with the gizmo
    // still usable — no player, so game input/possession is inert.
    void PlayInEditor(EPlayInEditorType type)
    {
        if (level == null || IsPlaying) return;
        _play_type = type;

        play_level = level.Clone();   // snapshot the current edited state
        DeinitLevel(level);           // reverse editor construction (lights/env) so PIE doesn't double them

        // fresh camera slot so only the clone's C3_Camera (if any) claims it
        C3_Camera.active = null;
        if (type == EPlayInEditorType.NORMAL)
        {
            _play_player = new ImpPlayer();
            ImpPlayer.s_active = _play_player;   // static input API + camera input-target registration
        }

        ImpPhysicsWorld.Init();       // spin up Jolt before components create their bodies in Begin
        PlayLevel(play_level);        // Init + Begin — full runtime lifecycle for the play instance

        pnl_world.level = play_level;
        pnl_world.game_player = _play_player;        // non-null (NORMAL) -> game-view rendering
        pnl_level_hierarchy.components = play_level.components;
        pnl_level_hierarchy.tint_instance = true;    // outliner tints the running instance blue
        ClearSelection();
    }

    // Stops the running session, discarding the play instance and restoring the editor preview.
    public void StopPlayInEditor()
    {
        if (!IsPlaying) return;

        EndPlayLevel(play_level!);    // OnEnd (bodies, input) while the physics world is still up
        ImpPhysicsWorld.Shutdown();
        play_level = null;
        _play_player = null;
        ImpPlayer.s_active = null;
        C3_Camera.active = null;      // drop the reference to the destroyed clone's camera

        pnl_world.level = level;
        pnl_world.game_player = null;
        pnl_level_hierarchy.components = level!.components;
        pnl_level_hierarchy.tint_instance = false;
        ClearSelection();
        InitLevel(level);             // rebuild editor construction only (no Begin/Update)
    }

    // ticks the running play instance only. Editor preview never runs OnUpdate — that is
    // runtime-only (otherwise e.g. C3_Camera_RotTest mouse look fires while editing).
    public void TickLevel(double delta)
    {
        if (play_level == null) return;

        ImpPhysicsWorld.Step(delta);   // advance the sim; components read results back in Update
        _play_player?.OnUpdate(delta);
        foreach (var c in play_level.components) c.Update(delta);
    }

    // Editor construction pass — OnInit only (lights, meshes, env). Never Begin/End/Update.
    static void InitLevel(A_Level lvl)
    {
        foreach (var c in lvl.components) c.Init();
    }

    // Reverse editor construction — OnDeinit only.
    static void DeinitLevel(A_Level lvl)
    {
        foreach (var c in lvl.components) c.Deinit();
    }

    // Runtime/PIE start — Init then Begin (physics bodies, active camera, input targets…).
    static void PlayLevel(A_Level lvl)
    {
        foreach (var c in lvl.components) { c.Init(); c.Begin(); }
    }

    // Runtime/PIE stop — OnEnd (bodies released while the world is still alive).
    static void EndPlayLevel(A_Level lvl)
    {
        foreach (var c in lvl.components) c.End();
    }

    // clears the shared selection (its entities belong to whichever instance we just switched away
    // from) and refreshes the inspector / gizmo targets
    void ClearSelection()
    {
        pnl_world.selection.Clear();
        OnSelectionChanged();
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
