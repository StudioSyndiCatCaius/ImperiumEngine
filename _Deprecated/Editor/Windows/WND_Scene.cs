using System.Numerics;
using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Windows;

//Scene editor
public class WND_Scene : EdWindow
{
    public C2_TabBox tab_scenes=new ()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        size_min = new(0, 120),
    };
    public C2_TabBox tab_outliners=new ()
    {
        size = new(200,0),
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        stretch_ratio = 1f,
        size_min = new(0, 80),
    };
    public C2_TabBox tab_inspectors=new ()
    {
        size = new(200,0),
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        stretch_ratio = 1.2f,
        size_min = new(0, 80),
    };

    public C2_Tree outliner = new()
    {
        name = "Outliner",
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };
    
    public C2_Inspector inspector_comp=new()
    {
        name = "Component",
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };
    public C2_Inspector inspector_scene=new()
    {
        name = "Scene",
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };

    ImpScene _bound_scene;
    ImpComp _selected_comp;
    string _hier_sig = "";
    public PNL_FileBrowser file_browser=new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        size_min = new(0, 200),
    };

    public static WND_Scene active;

    public WND_Scene()
    {
        active = this;
        name = "Scene";
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;

        C2_List list_scene_file = new()
        {
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            alignment = EUIAlignment.Vertical,
        };
        
        C2_List list_main=new()
        {
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            alignment = EUIAlignment.Horizontal,
        };
        Child_Add(list_main);
        
        
        
        C2_List list_panels=new()
        {
            size = new(300,0),
            size_min = new(180,0),
            alignment = EUIAlignment.Vertical,
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
        list_panels.Child_Add(tab_outliners);
        list_panels.Child_Add(new C2_Seperator { alignment = EUIAlignment.Vertical });
        list_panels.Child_Add(tab_inspectors);

        outliner.allow_reorder = true;
        outliner.on_item_drop = OnTreeDrop;
        inspector_comp.on_hierarchy_drop = OnHierarchyDrop;
        outliner.on_item_click = item =>
        {
            if (item.data is not ImpComp comp) return;
            _selected_comp = comp;
            inspector_comp.Objects_Add(new List<object> { comp }, true);
            tab_inspectors.selected_tab = 0;
            EdScene ed = ActiveEdScene();
            if (ed == null) return;
            ed.gizmo_data.on_selection_changed = null;
            ed.gizmo_data.Selection_Set(new[] { comp });
            ed.gizmo_data.on_selection_changed = OnGizmoSelection;
            if (comp is ImpComp2D) ed.edit_mode = ESceneEditorMode.Mode_2D;
            else if (comp is ImpComp3D) ed.edit_mode = ESceneEditorMode.Mode_3D;
        };
        tab_outliners.Child_Add(outliner);
        
        tab_inspectors.Child_Add(inspector_comp);
        tab_inspectors.Child_Add(inspector_scene);
        tab_inspectors.selected_tab = 1;
        
        C2_Expandable file_browser_wrap = new()
        {
            name = "File Browser",
            is_expanded = true,
            bar_height = 22,
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            size_min = new(0, 22),
            stretch_ratio = 0.5f,
        };
        file_browser_wrap.Child_Add(file_browser);

        list_scene_file.Child_Add(tab_scenes);
        list_scene_file.Child_Add(new C2_Seperator { alignment = EUIAlignment.Vertical });
        list_scene_file.Child_Add(file_browser_wrap);
        
        list_main.Child_Add(list_scene_file);
        list_main.Child_Add(new C2_Seperator { alignment = EUIAlignment.Horizontal });
        list_main.Child_Add(list_panels);

        ImpScene preview = new();
        preview.sky_texture = new TRef<A_TextureHDR>(new A_TextureHDR
        {
            source_file = ImpFile.GetOrCreate(ImpFile.Path_Resolve("{engine}/Textures/HDRI/sky_1.hdr")),
        });

        preview.root.name = preview.GetName();

        C3_Mesh ground = new() { name = "Ground", mesh = A_Mesh.GEO_PLANE };
        ground.Scale_Set(new Vector3(40f, 1f, 40f));

        C3_Mesh cube = new() { name = "Cube", mesh = A_Mesh.GEO_CUBE };
        cube.Position_Set(new Vector3(0f, 0.5f, 0f));

        C2_Box panel = new()
        {
            name = "Panel",
            size = new(320, 140),
            pivot = new Vector2(0.5f, 0.5f),
            normalize_pivot = true,
        };
        panel.Position_Set(new Vector2(80, 80));
        C2_Text title = new()
        {
            name = "Title",
            text = "Hello 2D",
            size = new(320, 36),
            style = UiStyle_Text.LIGHT,
            pivot = new Vector2(0.5f, 0.5f),
            normalize_pivot = true,
        };
        title.Position_Set(new Vector2(80, 36));

        preview.root.Child_Add(ground);
        preview.root.Child_Add(cube);
        preview.root.Child_Add(panel);
        preview.root.Child_Add(title);
        Scene_Add(preview);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        // Claimed here rather than in EdScene, because the hotkeys below edit the scene and run
        // before the tab itself updates - they have to land in the open scene's own history.
        EdScene active_ed = ActiveEdScene();
        if (active_ed != null && IsVisibleInTree())
        {
            active_ed.undo.asset = active_ed.scene;
            ImpUndo.active = active_ed.undo;
        }

        ImpScene scene = active_ed?.scene;
        if (scene != _bound_scene)
        {
            BindScene(scene);
            return;
        }
        if (scene == null) return;
        string sig = HierSig(scene.root);
        if (sig != _hier_sig) RefreshOutliner(false);
        HandleEditHotkeys();
    }

    void HandleEditHotkeys()
    {
        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];
        if (!is_visible) return;
        if (player.ui_focus is C2_TextEdit te && te.is_focused) return;

        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        if (ctrl && ImpPlayer.Key_IsPressed(EInputKey.Key_D)) DuplicateSelected();
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Delete)) DeleteSelected();
    }

    void OnTreeDrop(TTreeItem src, TTreeItem dst, ETreeDrop where)
    {
        if (src.data is ImpComp a && dst.data is ImpComp b)
            OnHierarchyDrop(a, b, where);
    }

    void OnHierarchyDrop(ImpComp a, ImpComp b, ETreeDrop where)
    {
        if (a == null || b == null) return;
        if (a == b || a.IsAncestorOf(b)) return;
        if (_bound_scene != null && a == _bound_scene.root) return;
        if (a.IsPackedForeign || b.IsPackedForeign) return;
        if (where == ETreeDrop.Child && b.IsInstanceRoot) return;

        TCompPlace from = ImpUndo.Place_Get(a);

        if (where == ETreeDrop.Child || b.parent == null)
        {
            a.Reparent(b);
            outliner.Tree_ExpandKey(C2_Tree.KeyOf(b), true);
        }
        else
        {
            ImpComp p = b.parent;
            int idx = p.children.IndexOf(b);
            if (where == ETreeDrop.After) idx++;
            a.Reparent(p, idx);
        }
        ImpUndo.Comp_Moved(a, from, "Reparent");
        _selected_comp = a;
        inspector_comp.Properties_Rebuild();
    }

    static List<ImpComp> SelectionRoots(List<ImpComp> comps)
    {
        List<ImpComp> roots = new();
        if (comps == null) return roots;
        for (int i = 0; i < comps.Count; i++)
        {
            ImpComp c = comps[i];
            if (c == null) continue;
            bool nested = false;
            for (ImpComp p = c.parent; p != null; p = p.parent)
            {
                if (comps.Contains(p)) { nested = true; break; }
            }
            if (!nested) roots.Add(c);
        }
        return roots;
    }

    void DuplicateSelected()
    {
        EdScene ed = ActiveEdScene();
        if (ed?.scene?.root == null) return;
        List<ImpComp> src = SelectionRoots(ed.gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(ed.gizmo_data.selected_comps);
        List<ImpComp> copies = new();
        ImpUndo.Group_Begin("Duplicate");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == ed.scene.root || s.parent == null || s.IsPackedForeign) continue;
            ImpComp copy = s.Clone();
            if (copy == null) continue;
            // Named before the insert below, so the scan never sees the copy itself. The source
            // is still in there under its own name, so the desired name always collides and
            // always picks up a number: mesh -> mesh1 -> mesh2.
            copy.name = ImpComp.Name_Unique(s.parent, string.IsNullOrEmpty(s.name) ? copy.GetType().Name : s.name);
            int idx = s.parent.children.IndexOf(s);
            s.parent.Child_Insert(idx + 1, copy);
            if (copy is ImpComp3D c3) c3.Position_Set(c3.Position_Get(false) + new Vector3(0.5f, 0f, 0f), false);
            if (copy is ImpComp2D c2) c2.Position_Set(c2.Position_Get(false) + new Vector2(16f, 16f), false);
            copies.Add(copy);
            // Nothing to come back from - undo takes the copy back out of the scene.
            ImpUndo.Comp_Moved(copy, default, "Duplicate");
        }
        if (copies.Count > 0)
        {
            SelectionUndo(ed, was_selected, copies);
            ed.gizmo_data.Selection_Set(copies);
        }
        ImpUndo.Group_End();
    }

    void DeleteSelected()
    {
        EdScene ed = ActiveEdScene();
        if (ed?.scene?.root == null) return;
        List<ImpComp> src = SelectionRoots(ed.gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(ed.gizmo_data.selected_comps);
        ImpUndo.Group_Begin("Delete");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == ed.scene.root || s.IsPackedForeign) continue;
            // Detach rather than Destroy: Destroy tears the subtree apart child by child, and
            // undo needs the comp to come back with everything under it still attached.
            TCompPlace from = ImpUndo.Place_Get(s);
            s.Detach();
            ImpUndo.Comp_Moved(s, from, "Delete");
        }
        SelectionUndo(ed, was_selected, new List<ImpComp>());
        ed.gizmo_data.Selection_Clear();
        ImpUndo.Group_End();
    }

    // Selection is part of the edit: undoing a delete should hand back what was deleted, and
    // undoing a duplicate must not leave the gizmo driving comps that are no longer in the scene.
    static void SelectionUndo(EdScene ed, List<ImpComp> before, List<ImpComp> after)
    {
        ImpUndo.Push("Selection",
            () => ed.gizmo_data.Selection_Set(before),
            () => ed.gizmo_data.Selection_Set(after));
    }
    
    // -------------------------------------------------------------------
    // Scene
    // -------------------------------------------------------------------
    public void Scene_Add(ImpScene scene)
    {
        if (scene == null) return;
        int page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] == tab_scenes.list_tabs) continue;
            if (tab_scenes.children[i] is EdScene ed && SameScene(ed.scene, scene))
            {
                tab_scenes.selected_tab = page;
                return;
            }
            page++;
        }

        EdScene ui_scene = new()
        {
            name = scene.GetName(),
            scene = scene,
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
        ui_scene.sceneView.scene = scene;
        ui_scene.gizmo_data.on_selection_changed = OnGizmoSelection;
        tab_scenes.Child_Add(ui_scene);
        tab_scenes.selected_tab = page;
        BindScene(scene);
    }

    void OnGizmoSelection()
    {
        EdScene ed = ActiveEdScene();
        if (ed == null) return;
        List<ImpComp> sel = ed.gizmo_data.selected_comps;
        _selected_comp = sel.Count > 0 ? sel[0] : null;
        if (_selected_comp != null)
        {
            List<object> objs = new();
            for (int i = 0; i < sel.Count; i++) objs.Add(sel[i]);
            inspector_comp.Objects_Add(objs, true);
            tab_inspectors.selected_tab = 0;
            outliner.Tree_SelectData(_selected_comp);
        }
        else
        {
            inspector_comp.Objects_Clear();
            outliner.Tree_SelectData(null);
        }
    }

    EdScene ActiveEdScene()
    {
        int page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] == tab_scenes.list_tabs) continue;
            if (tab_scenes.children[i] is not EdScene ed) continue;
            if (page == tab_scenes.selected_tab) return ed;
            page++;
        }
        return null;
    }

    void BindScene(ImpScene scene)
    {
        _bound_scene = scene;
        _selected_comp = null;
        inspector_scene.Objects_Clear();
        inspector_comp.Objects_Clear();
        if (scene != null) inspector_scene.Object_Add(scene, true);
        tab_inspectors.selected_tab = 1;
        RefreshOutliner(true);
        EdScene ed = ActiveEdScene();
        if (ed != null)
        {
            ed.gizmo_data.on_selection_changed = OnGizmoSelection;
            OnGizmoSelection();
        }
    }

    void RefreshOutliner(bool expand_all)
    {
        ImpScene scene = _bound_scene;
        _hier_sig = scene?.root != null ? HierSig(scene.root) : "";
        object keep = _selected_comp ?? outliner.selected_data;
        if (scene?.root == null)
        {
            outliner.Tree_Clear();
            return;
        }
        outliner.Tree_Populate_FromComp(scene.root);
        if (expand_all) outliner.Tree_ExpandAll(true);
        if (keep != null) outliner.Tree_SelectData(keep);
    }

    static string HierSig(ImpComp root)
    {
        System.Text.StringBuilder sb = new();
        void Walk(ImpComp c)
        {
            if (c == null) return;
            sb.Append(c.GetHashCode()).Append(':').Append(c.name ?? "").Append(':').Append(c.children.Count).Append(';');
            for (int i = 0; i < c.children.Count; i++) Walk(c.children[i]);
        }
        Walk(root);
        return sb.ToString();
    }

    static bool SameScene(ImpScene a, ImpScene b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        if (string.IsNullOrEmpty(a.filepath) || string.IsNullOrEmpty(b.filepath)) return false;
        try { return string.Equals(Path.GetFullPath(a.filepath), Path.GetFullPath(b.filepath), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a.filepath, b.filepath, StringComparison.OrdinalIgnoreCase); }
    }
}

public class EdScene : C2_Box
{
    public ImpScene scene;

    //one history per scene tab, so undo in one scene never reaches into another
    public ImpUndo undo = new();

    public ESceneEditorMode edit_mode = ESceneEditorMode.Mode_3D;
    public TGizmoData gizmo_data = new() { space = EGizmoSpace.Local };
    public EGizmoMode gizmo_mode
    {
        get => gizmo_data.mode;
        set => gizmo_data.mode = value;
    }
    public EGizmoSpace gizmo_orientation
    {
        get => gizmo_data.space;
        set => gizmo_data.space = value;
    }

    C3_Gizmo gizmo_3d = new();
    C2_Gizmo gizmo_2d = new();

    public C2_SceneView sceneView = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };

    C2_List toolbar = new()
    {
        alignment = EUIAlignment.Horizontal,
        spacing = 2,
        size = new(0, 26),
        size_min = new(0, 26),
        view_alighnment_H = EUIViewportAlignment.Fill,
    };

    C2_Button btn_3d, btn_2d, btn_move, btn_rot, btn_scale, btn_local, btn_world;
    C2_Slider snap_slider;

    public EdScene()
    {
        gizmo_3d.gizmo_data = gizmo_data;
        gizmo_2d.gizmo_data = gizmo_data;
        sceneView.gizmo_data = gizmo_data;
        sceneView.gizmo_3d = gizmo_3d;
        sceneView.gizmo_2d = gizmo_2d;

        btn_3d = ToolBtn("3D", () => edit_mode = ESceneEditorMode.Mode_3D);
        btn_2d = ToolBtn("2D", () => edit_mode = ESceneEditorMode.Mode_2D);
        toolbar.Child_Add(ToolSep());
        btn_move = ToolBtn("Move", () => gizmo_mode = EGizmoMode.Translate);
        btn_rot = ToolBtn("Rot", () => gizmo_mode = EGizmoMode.Rotate);
        btn_scale = ToolBtn("Scale", () => gizmo_mode = EGizmoMode.Scale);
        toolbar.Child_Add(ToolSep());
        btn_local = ToolBtn("Local", () => gizmo_orientation = EGizmoSpace.Local);
        btn_world = ToolBtn("World", () => gizmo_orientation = EGizmoSpace.World);
        toolbar.Child_Add(ToolSep());
        toolbar.Child_Add(new C2_Text
        {
            text = "Snap",
            style = UiStyle_Text.MUTED,
            wrap = ETextWrap.None,
            size = new(36, 22),
            size_min = new(32, 22),
            view_alighnment_V = EUIViewportAlignment.Center,
            text_alignment_h = EUIPositionAlignment.End,
            text_alignment_v = EUIPositionAlignment.Center,
        });
        snap_slider = new C2_Slider
        {
            is_spinner = true,
            min = 0,
            max = 0,
            value = gizmo_data.snap_translate,
            value_text_decimals = 2,
            drag_sensitivity = 0.05f,
            size = new(72, 22),
            size_min = new(56, 22),
            view_alighnment_V = EUIViewportAlignment.Center,
        };
        snap_slider.on_changed = s =>
        {
            float v = MathF.Max(0.001f, s.value);
            if (edit_mode == ESceneEditorMode.Mode_2D) gizmo_data.snap_translate_2d = v;
            else gizmo_data.snap_translate = v;
        };
        toolbar.Child_Add(snap_slider);

        C2_List root = new()
        {
            alignment = EUIAlignment.Vertical,
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
        root.Child_Add(toolbar);
        root.Child_Add(sceneView);
        Child_Add(root);

        if (ImpPlayer.players.Count > 0)
            ImpPlayer.players[0].ui_focus = sceneView;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        sceneView.edit_mode = edit_mode;
        sceneView.scene = scene;
        Paint(btn_3d, edit_mode == ESceneEditorMode.Mode_3D);
        Paint(btn_2d, edit_mode == ESceneEditorMode.Mode_2D);
        Paint(btn_move, gizmo_mode == EGizmoMode.Translate);
        Paint(btn_rot, gizmo_mode == EGizmoMode.Rotate);
        Paint(btn_scale, gizmo_mode == EGizmoMode.Scale);
        Paint(btn_local, gizmo_orientation == EGizmoSpace.Local);
        Paint(btn_world, gizmo_orientation == EGizmoSpace.World);

        if (snap_slider != null && !snap_slider.IsBusy)
        {
            bool two = edit_mode == ESceneEditorMode.Mode_2D;
            snap_slider.value_text_decimals = two ? 0 : 2;
            snap_slider.step = two ? 1f : 0f;
            snap_slider.drag_sensitivity = two ? 0.2f : 0.05f;
            snap_slider.Value_SetQuiet(two ? gizmo_data.snap_translate_2d : gizmo_data.snap_translate);
        }

        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];
        if (!is_visible) return;
        if (player.ui_focus is C2_TextEdit te && te.is_focused) return;

        bool over = player.Cursor_IsInDimensions(Dimensions_Get());
        bool ours = player.ui_focus == sceneView || IsChildFocus(player.ui_focus);
        if (!over && !ours) return;

        bool cam = sceneView.IsCameraBusy;
        if (!cam)
        {
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_W)) gizmo_mode = EGizmoMode.Translate;
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_E)) gizmo_mode = EGizmoMode.Rotate;
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_R)) gizmo_mode = EGizmoMode.Scale;
        }
        if (!cam && ImpPlayer.Key_IsPressed(EInputKey.Key_F)) sceneView.Focus_Selection();
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_T))
            gizmo_orientation = gizmo_orientation == EGizmoSpace.Local ? EGizmoSpace.World : EGizmoSpace.Local;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Tab))
            edit_mode = edit_mode == ESceneEditorMode.Mode_3D ? ESceneEditorMode.Mode_2D : ESceneEditorMode.Mode_3D;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_1)) edit_mode = ESceneEditorMode.Mode_3D;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_2)) edit_mode = ESceneEditorMode.Mode_2D;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Escape)) gizmo_data.Selection_Clear();
    }

    bool IsChildFocus(ImpComp focus)
    {
        ImpComp n = focus;
        while (n != null)
        {
            if (n == this) return true;
            n = n.parent;
        }
        return false;
    }

    C2_Button ToolBtn(string text, Action on_click)
    {
        C2_Button b = new()
        {
            text = text,
            on_click = on_click,
            style = new UiStyle_Button(),
            text_style = UiStyle_Text.LIGHT,
            override_font_size = 11,
            content_pad = 2,
            size = new(48, 22),
            size_min = new(44, 22),
            view_alighnment_V = EUIViewportAlignment.Center,
        };
        toolbar.Child_Add(b);
        return b;
    }

    static C2_Seperator ToolSep()
    {
        return new C2_Seperator
        {
            alignment = EUIAlignment.Horizontal,
            is_draggable = false,
            thickness = 8,
            size = new(8, 22),
            view_alighnment_V = EUIViewportAlignment.Fill,
        };
    }

    static void Paint(C2_Button b, bool on)
    {
        if (b?.style == null) return;
        b.style.style_unhovered = on ? UiStyle_Box.STYLE_BTN_PRESS : UiStyle_Box.STYLE_BTN_IDLE;
        b.style.style_hovered = on ? UiStyle_Box.STYLE_BTN_PRESS : UiStyle_Box.STYLE_BTN_HOVER;
    }
}