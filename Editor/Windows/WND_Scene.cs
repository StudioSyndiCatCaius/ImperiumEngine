using System.Numerics;
using System.Reflection;
using Editor.Dialog;
using Editor.Panels;
using Editor.Scenes;
using Editor.UI;
using Engine;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor.Windows;

public class WND_Scene : EdWindow
{
    
    public static WND_Scene? active;

    public PNL_Inspector inspector_comp = new() { title = "Comp" };
    public PNL_Inspector inspector_scene = new() { title = "Scene" };
    public PNL_SceneTree scene_tree = new() { title = "Outliner" };
    public PNL_SceneDebug scene_debug = new() { title = "Debug" };

    public List<EUI_SceneTab> scene_tabs = new();
    public EUI_SceneTab? current_scene_tab = null;
    public ImpComp? selected_comp;

    [EdConfig] public float right_panel_width = 300f;
    bool _booted;

    static List<Type>? _common_3d;
    static List<Type>? _common_2d;

    public WND_Scene()
    {
        active = this;
        scene_tree.on_select = Select;
        scene_tree.on_delete = DeleteComp;
        scene_tree.on_duplicate = DuplicateComp;
        scene_tree.on_add_child = AddChild;
        scene_tree.on_move = MoveComp;
        inspector_comp.on_select = Select;
        scene_tree.on_rename = c => inspector_comp.BeginRename(c);
    }

    public void Tick(double dt)
    {
        if (!_booted)
        {
            _booted = true;
            if (!EdConfig.RestoreScenes(this))
                Scene_New();
        }

        scene_debug.BeginFrame();
        A_Scene? s = current_scene_tab?.scene;
        if (s == null) return;
        if (s.root != null) s.root.scene = s;
        if (s.viewport == null)
            s.viewport = new ImpViewport { size = new Vector2(EUI_Viewport2D.CanvasW, EUI_Viewport2D.CanvasH) };
        scene_debug.ProfileUpdate(s, dt);
    }

    public override void OnDraw()
    {
        base.OnDraw();
        if (!_booted)
        {
            _booted = true;
            if (!EdConfig.RestoreScenes(this))
                Scene_New();
        }
        BindPanels();

        float avail_x = ImGui.GetContentRegionAvail().X;
        float splitter = 6f;
        right_panel_width = Math.Clamp(right_panel_width, 180f, MathF.Max(180f, avail_x - 160f));
        float left_w = MathF.Max(80f, avail_x - right_panel_width - splitter);

        ImGui.BeginChild("left", new Vector2(left_w, 0), false);
        DrawSceneTabs();
        ImGui.EndChild();

        ImGui.SameLine(0, 0);
        ImGui.InvisibleButton("##scene_split", new Vector2(splitter, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive())
            right_panel_width -= ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);

        ImGui.SameLine(0, 0);
        ImGui.BeginChild("right", new Vector2(0, 0), false);

        float availY = ImGui.GetContentRegionAvail().Y;
        float gap = ImGui.GetStyle().ItemSpacing.Y;
        float h = MathF.Max(0f, (availY - gap) * 0.5f);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.BeginChild("targa", new Vector2(0, h), false);
        ImGui.PopStyleVar();
        uint outliner_dock = ImGui.GetID("outliner_dock");
        ImGui.DockSpace(outliner_dock);
        ImGui.EndChild();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.BeginChild("targo", new Vector2(0, h), false);
        ImGui.PopStyleVar();
        uint inspector_dock = ImGui.GetID("inspector_dock");
        ImGui.DockSpace(inspector_dock);
        ImGui.EndChild();

        ImGui.EndChild();

        BindPanels();

        ImGui.SetNextWindowDockID(outliner_dock, ImGuiCond.Always);
        if (ImGui.Begin("Common"))
            DrawCommon();
        ImGui.End();

        scene_tree.dock_id = outliner_dock;
        scene_tree.OnDraw();

        scene_debug.dock_id = outliner_dock;
        scene_debug.OnDraw();

        inspector_scene.dock_id = inspector_dock;
        inspector_scene.OnDraw();

        inspector_comp.dock_id = inspector_dock;
        inspector_comp.OnDraw();

        HandleHotkeys();
    }

    void DrawSceneTabs()
    {
        if (scene_tabs.Count == 0)
        {
            ImGui.TextDisabled("No scene open");
            if (ImGui.Button("New Scene"))
                Scene_New();
            return;
        }

        if (!ImGui.BeginTabBar("##scene_tabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs))
            return;

        EUI_SceneTab? visible = current_scene_tab;
        for (int i = 0; i < scene_tabs.Count; i++)
        {
            EUI_SceneTab tab = scene_tabs[i];
            bool open = true;
            string name = TabName(tab);
            ImGuiTabItemFlags flags = tab.scene != null && tab.scene.is_dity
                ? ImGuiTabItemFlags.UnsavedDocument
                : ImGuiTabItemFlags.None;
            if (!ImGui.BeginTabItem(name + "###s" + tab.GetHashCode(), ref open, flags))
            {
                if (!open) Scene_Close(tab.scene);
                continue;
            }

            visible = tab;
            tab.OnDraw();
            ImGui.EndTabItem();
            if (!open) Scene_Close(tab.scene);
        }
        ImGui.EndTabBar();

        if (visible != current_scene_tab)
        {
            current_scene_tab = visible;
            BindPanels();
        }
    }

    static string TabName(EUI_SceneTab tab)
    {
        if (tab.scene == null) return "Scene";
        if (string.IsNullOrEmpty(tab.scene.filepath)) return "Untitled";
        return Path.GetFileNameWithoutExtension(tab.scene.filepath);
    }

    public void BindPanels()
    {
        A_Scene? s = current_scene_tab?.scene;
        s?.environment?.Refresh(false);
        ImpComp? outline = OutlinerRoot(selected_comp);
        scene_tree.scene = s;
        scene_tree.selected = outline;
        inspector_scene.selected_object = s;
        inspector_comp.tree_root = outline;
        inspector_comp.selected_object = selected_comp;
        if (current_scene_tab != null)
        {
            current_scene_tab.viewport3d.scene = s;
            current_scene_tab.viewport3d.selected = selected_comp;
            current_scene_tab.viewport3d.on_select = Select;
            current_scene_tab.viewport2d.scene = s;
            current_scene_tab.viewport2d.selected = selected_comp;
            current_scene_tab.viewport2d.on_select = Select;
            current_scene_tab.script_graph.script = s?.script;
            current_scene_tab.script_graph.on_changed = () => { if (s != null) s.is_dity = true; };
        }
    }

    void HandleHotkeys()
    {
        if (ImGui.GetIO().WantTextInput) return;
        EUI_SceneTab? tab = current_scene_tab;
        if (tab == null) return;

        bool over_view = tab.viewport3d.hovered || tab.viewport2d.hovered;
        if (over_view && !tab.viewport3d.IsCameraBusy && !tab.viewport3d.gizmo.busy && !tab.viewport2d.gizmo.busy)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) tab.gizmo_mode = EEditorGizmo_Mode.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) tab.gizmo_mode = EEditorGizmo_Mode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) tab.gizmo_mode = EEditorGizmo_Mode.Scale;
            if (ImGui.IsKeyPressed(ImGuiKey.Tab))
                tab.view = tab.view == ESceneEditView.Mode_3D ? ESceneEditView.Mode_2D : ESceneEditView.Mode_3D;
            if (ImGui.IsKeyPressed(ImGuiKey._1)) tab.view = ESceneEditView.Mode_3D;
            if (ImGui.IsKeyPressed(ImGuiKey._2)) tab.view = ESceneEditView.Mode_2D;
            if (ImGui.IsKeyPressed(ImGuiKey.F))
            {
                if (tab.view == ESceneEditView.Mode_3D) tab.viewport3d.Focus(selected_comp);
                else tab.viewport2d.Focus(selected_comp);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.T))
                tab.gizmo_orientation = tab.gizmo_orientation == EEditorGizmo_Orientation.Local
                    ? EEditorGizmo_Orientation.World
                    : EEditorGizmo_Orientation.Local;
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                Select(null);
        }

        ImGuiIOPtr io = ImGui.GetIO();
        if (ImGui.IsKeyPressed(ImGuiKey.Delete) && selected_comp != null)
            DeleteComp(selected_comp);
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.D) && selected_comp != null)
            DuplicateComp(selected_comp);
        if (io.KeyShift && !io.KeyCtrl && !io.KeyAlt && ImGui.IsKeyPressed(ImGuiKey.N))
        {
            ImpComp? parent = selected_comp;
            if (parent == null || !parent.Allow_Children() || parent.Editor_IsLocked() || parent.is_builtin)
                parent = tab.scene?.root;
            scene_tree.OpenAdd(parent);
        }
    }

    void DrawCommon()
    {
        EnsureCommon();
        ImGui.TextDisabled("Double-click to add to the selected comp");
        ImGui.SeparatorText("3D");
        DrawCommonList(_common_3d!);
        ImGui.SeparatorText("2D");
        DrawCommonList(_common_2d!);
    }

    void DrawCommonList(List<Type> types)
    {
        for (int i = 0; i < types.Count; i++)
        {
            Type t = types[i];
            ImGui.Selectable(t.Name);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                ImpComp? parent = selected_comp ?? current_scene_tab?.scene?.root;
                if (parent != null && !parent.Editor_IsLocked()) AddChild(parent, t);
            }
        }
    }

    static void EnsureCommon()
    {
        if (_common_3d != null) return;
        _common_3d = new();
        _common_2d = new();
        GType.ForEachOf(typeof(Imp3D), (t, _) =>
        {
            if (t.IsAbstract) return;
            ImpClassAttribute? a = t.GetCustomAttribute<ImpClassAttribute>();
            if (a is { Hidden: true } || a is not { Common: true }) return;
            _common_3d.Add(t);
        }, include_base: false);
        GType.ForEachOf(typeof(Imp2D), (t, _) =>
        {
            if (t.IsAbstract) return;
            ImpClassAttribute? a = t.GetCustomAttribute<ImpClassAttribute>();
            if (a is { Hidden: true } || a is not { Common: true }) return;
            _common_2d.Add(t);
        }, include_base: false);
        _common_3d.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _common_2d.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    public void Select(ImpComp? comp)
    {
        if (comp != null && comp.Editor_IsLocked())
            comp = null;
        selected_comp = comp;
        ImpComp? outline = OutlinerRoot(comp);
        inspector_comp.selected_object = comp;
        inspector_comp.tree_root = outline;
        scene_tree.selected = outline;
        if (current_scene_tab == null || comp == null) return;
        if (comp is Imp2D) current_scene_tab.view = ESceneEditView.Mode_2D;
        else if (comp is Imp3D) current_scene_tab.view = ESceneEditView.Mode_3D;
    }

    static ImpComp? OutlinerRoot(ImpComp? c)
    {
        while (c != null && c.parent != null && (c.is_builtin || c.is_child_of_prefab))
            c = c.parent;
        return c;
    }

    public void MoveComp(ImpComp src, ImpComp new_parent, int idx)
    {
        if (src == null || new_parent == null || src.parent == null) return;
        if (src == new_parent || new_parent.Is_ChildOf(src) || src.is_builtin) return;
        if (src.Editor_IsLocked() || new_parent.Editor_IsLocked()) return;
        ImpComp old_parent = src.parent;
        int old_idx = old_parent.children.IndexOf(src);
        if (old_idx < 0) return;
        if (old_parent == new_parent && old_idx == idx) return;

        Imp3D? c3 = src as Imp3D;
        Imp2D? c2 = src as Imp2D;
        TTransform3 old_l3 = c3 != null ? c3.transform : default;
        TTransform3 world3 = c3 != null ? c3.global_transform : default;
        TLayout2 old_lay = c2 != null ? c2.layout : default;
        TBounds2 kept2 = c2 != null ? c2.bounds : default;

        new_parent.Child_Insert(src, idx);
        if (old_parent != new_parent)
        {
            if (c3 != null) c3.Transform_Set(world3, true);
            if (c2 != null && !kept2.IsEmpty)
            {
                c2.bounds = c2.Bounds_Cache();
                c2.layout.position += kept2.start - c2.bounds.start;
                c2.bounds = c2.Bounds_Cache();
            }
        }
        TTransform3 new_l3 = c3 != null ? c3.transform : default;
        TLayout2 new_lay = c2 != null ? c2.layout : default;

        if (new_parent.scene != null) new_parent.scene.is_dity = true;
        bool reparent = old_parent != new_parent;
        Editor.history.Record(
            () =>
            {
                old_parent.Child_Insert(src, old_idx);
                if (!reparent) return;
                if (c3 != null) c3.Transform_Set(old_l3, false);
                if (c2 != null) { c2.layout = old_lay; c2.bounds = c2.Bounds_Cache(); }
            },
            () =>
            {
                new_parent.Child_Insert(src, idx);
                if (!reparent) return;
                if (c3 != null) c3.Transform_Set(new_l3, false);
                if (c2 != null) { c2.layout = new_lay; c2.bounds = c2.Bounds_Cache(); }
            });
    }

    public void AddChild(ImpComp parent, Type type)
    {
        if (parent == null || type == null) return;
        if (parent.Editor_IsLocked()) return;
        if (!typeof(ImpComp).IsAssignableFrom(type) || type.IsAbstract) return;
        if (Activator.CreateInstance(type) is not ImpComp n) return;
        n.name = UniqueName(parent, type.Name);
        parent.Child_Add(n);
        if (parent.scene != null) parent.scene.is_dity = true;
        Editor.history.Record(
            () => { parent.children.Remove(n); n.parent = null; n.AssignScene(null, true); },
            () => { if (!parent.children.Contains(n)) parent.Child_Add(n); });
        Select(n);
    }

    public void DeleteComp(ImpComp s)
    {
        A_Scene? sc = current_scene_tab?.scene;
        if (s == null || sc == null || s == sc.root || s.is_builtin) return;
        ImpComp? parent = s.parent;
        if (parent == null) return;
        if (s.Editor_IsLocked()) return;
        string n = string.IsNullOrEmpty(s.name) ? s.GetType().Name : s.name;
        EDLG_Confirm.Run("Delete '" + n + "'?", ok =>
        {
            if (!ok) return;
            int idx = parent.children.IndexOf(s);
            parent.children.Remove(s);
            s.parent = null;
            s.AssignScene(null, true);
            sc.is_dity = true;
            Editor.history.Record(
                () =>
                {
                    if (!parent.children.Contains(s))
                    {
                        if (idx < 0 || idx > parent.children.Count) parent.children.Add(s);
                        else parent.children.Insert(idx, s);
                        s.parent = parent;
                        s.AssignScene(parent.scene, true);
                    }
                },
                () =>
                {
                    parent.children.Remove(s);
                    s.parent = null;
                    s.AssignScene(null, true);
                });
            Select(parent);
        });
    }

    public void DuplicateComp(ImpComp s)
    {
        A_Scene? sc = current_scene_tab?.scene;
        if (s == null || sc == null || s == sc.root || s.parent == null || s.is_builtin) return;
        if (s.Editor_IsLocked() || s.parent.Editor_IsLocked()) return;
        ImpComp copy = ImpComp.From_Table(s.To_Table(sc), sc);
        copy.Id_RenewTree();
        copy.name = UniqueName(s.parent, string.IsNullOrEmpty(s.name) ? copy.GetType().Name : s.name);
        int idx = s.parent.children.IndexOf(s);
        s.parent.Child_Add(copy);
        s.parent.children.Remove(copy);
        s.parent.children.Insert(idx + 1, copy);
        copy.parent = s.parent;
        copy.scene = sc;
        if (copy is Imp3D c3) c3.Position_Set(c3.transform.position + new Vector3(0.5f, 0f, 0f), false);
        if (copy is Imp2D c2)
        {
            c2.layout.position += new Vector2(16f, 16f);
            c2.bounds = c2.Bounds_Cache();
        }
        sc.is_dity = true;
        Editor.history.Record(
            () => { s.parent.children.Remove(copy); copy.parent = null; copy.AssignScene(null, true); },
            () =>
            {
                if (!s.parent.children.Contains(copy))
                {
                    s.parent.children.Insert(Math.Min(idx + 1, s.parent.children.Count), copy);
                    copy.parent = s.parent;
                    copy.AssignScene(sc, true);
                }
            });
        Select(copy);
    }

    static string UniqueName(ImpComp parent, string base_name)
    {
        string stem = base_name ?? "";
        int cut = stem.Length;
        while (cut > 0 && char.IsDigit(stem[cut - 1])) cut--;
        if (cut > 0) stem = stem[..cut];

        string n = stem;
        int i = 1;
        while (true)
        {
            bool hit = false;
            for (int k = 0; k < parent.children.Count; k++)
            {
                if (parent.children[k].name == n) { hit = true; break; }
            }
            if (!hit) return n;
            n = stem + i;
            i++;
        }
    }

    // ======================================================================================
    // Scene
    // ======================================================================================

    public void Scene_New()
    {
        A_Scene s = new();
        s.root = new ImpComp { name = "Root", scene = s };

        C3_Mesh ground = new() { name = "Ground" };
        A_Mesh? plane = A_Mesh.PLANE;
        if (plane != null) ground.mesh = plane;
        ground.Scale_Set(new Vector3(40f, 1f, 40f));

        C3_Mesh cube = new() { name = "Cube" };
        cube.Position_Set(new Vector3(0f, 0.5f, 0f));

        C2_Text title = new("Hello 2D") { name = "Title" };
        title.layout.size = new Vector2(320, 36);
        title.layout.position = new Vector2(80, 80);

        s.root.Child_Add(ground);
        s.root.Child_Add(cube);
        s.root.Child_Add(title);
        s.script = A_Script.MakeTest();
        s.is_dity = true;
        Scene_Open(s);
    }

    public void Scene_Open(A_Scene scene)
    {
        if (scene == null) return;
        foreach (EUI_SceneTab tab in scene_tabs)
        {
            if (tab.scene == scene)
            {
                current_scene_tab = tab;
                BindPanels();
                return;
            }
        }
        current_scene_tab = new EUI_SceneTab { scene = scene };
        scene_tabs.Add(current_scene_tab);
        // Edit mode only. Begin() starts the game mode and must not mutate the authored scene.
        scene.Refresh();
        if (scene.root != null) scene.root.AssignScene(scene);
        BindPanels();
        SaveSession();
    }

    public void Scene_Close(A_Scene? scene)
    {
        if (scene == null) return;
        if (scene.is_dity)
        {
            EDLG_Confirm.Run("Scene is unsaved. Close anyway?", b =>
            {
                if (b) Scene_CloseConfirm(scene);
            });
        }
        else Scene_CloseConfirm(scene);
    }

    private void Scene_CloseConfirm(A_Scene scene)
    {
        for (int i = 0; i < scene_tabs.Count; i++)
        {
            if (scene_tabs[i].scene != scene) continue;
            bool was = current_scene_tab == scene_tabs[i];
            scene_tabs.RemoveAt(i);
            if (was)
                current_scene_tab = scene_tabs.Count > 0
                    ? scene_tabs[Math.Min(i, scene_tabs.Count - 1)]
                    : null;
            if (current_scene_tab == null) selected_comp = null;
            BindPanels();
            SaveSession();
            return;
        }
    }

    static void SaveSession()
    {
        if (EdConfig.restoring) return;
        if (App.scene_current?.root is SNC_Editor_Root root)
            EdConfig.Save(root);
    }
}

// ####################################################################################################################
// Scene Tab
// ####################################################################################################################

public enum ESceneEditView
{
    [Title("3D")] Mode_3D,
    [Title("2D")] Mode_2D,
}
public enum ESceneEditMode
{
    Comps,
    Landscape,
}
public enum EEditorGizmo_Mode { Translate, Rotate, Scale }
public enum EEditorGizmo_Orientation { Local, World }

public class EUI_SceneTab : EdUi
{
    public A_Scene? scene;

    public EUI_Viewport3D viewport3d = new();
    public EUI_Viewport2D viewport2d = new();
    
    public PNL_ScriptGraph script_graph = new();

    public EUI_EnumToggle enumtoggle_edit_mode = new();
    public EUI_EnumToggle enumtoggle_view = new();
    public EUI_EnumToggle enumtoggle_gizmo_mode = new();
    public EUI_EnumToggle enumtoggle_gizmo_orientation = new();

    public ESceneEditMode edit_mode = ESceneEditMode.Comps;
    public ESceneEditView view = ESceneEditView.Mode_3D;
    public EEditorGizmo_Mode gizmo_mode = EEditorGizmo_Mode.Translate;
    public EEditorGizmo_Orientation gizmo_orientation = EEditorGizmo_Orientation.Local;

    public EUI_SceneTab()
    {
        enumtoggle_edit_mode.on_changed = e => edit_mode = (ESceneEditMode)e;
        enumtoggle_view.on_changed = e => view = (ESceneEditView)e;
        enumtoggle_gizmo_mode.on_changed = e => gizmo_mode = (EEditorGizmo_Mode)e;
        enumtoggle_gizmo_orientation.on_changed = e => gizmo_orientation = (EEditorGizmo_Orientation)e;
    }

    public override void OnDraw()
    {
        base.OnDraw();
        if (!ImGui.BeginTabBar("##scene_script"))
            return;

        if (ImGui.BeginTabItem("Scene"))
        {
            DrawScene();
            ImGui.EndTabItem();
        }
        else
        {
            viewport3d.hovered = false;
            viewport2d.hovered = false;
        }

        if (ImGui.BeginTabItem("Script"))
        {
            if (scene != null && scene.script == null)
            {
                scene.script = A_Script.MakeTest();
                scene.is_dity = true;
            }
            script_graph.script = scene?.script;
            script_graph.on_changed = () => { if (scene != null) scene.is_dity = true; };
            script_graph.OnDrawPanel();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    void DrawScene()
    {
        viewport3d.scene = scene;
        viewport3d.gizmo_mode = gizmo_mode;
        viewport3d.gizmo_orientation = gizmo_orientation;
        viewport2d.scene = scene;
        viewport2d.gizmo_mode = gizmo_mode;
        viewport2d.gizmo_orientation = gizmo_orientation;

        enumtoggle_edit_mode.value = edit_mode;
        enumtoggle_edit_mode.OnDraw();
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        enumtoggle_view.value = view;
        enumtoggle_view.OnDraw();
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        enumtoggle_gizmo_mode.value = gizmo_mode;
        enumtoggle_gizmo_mode.OnDraw();
        ImGui.SameLine();
        enumtoggle_gizmo_orientation.value = gizmo_orientation;
        enumtoggle_gizmo_orientation.OnDraw();

        ImGui.BeginChild("##scene_view", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (view == ESceneEditView.Mode_3D)
            viewport3d.OnDraw();
        else
            viewport2d.OnDraw();
        ImGui.EndChild();
    }
}
