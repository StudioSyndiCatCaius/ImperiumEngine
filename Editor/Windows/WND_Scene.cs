using System.Numerics;
using Editor.Dialog;
using Editor.Panel;
using Editor.Scenes;
using ImperiumEngine;
using Raylib_cs;
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
        layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
                size_min = new(0, 120),
            },
        };
    public C2_TabBox tab_outliners=new ()
    {
        stretch_ratio = 1f,
        layout = new TLayout2
            {
                size = new(200,0),
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
                size_min = new(0, 80),
            },
        };
    public C2_TabBox tab_inspectors=new ()
    {
        stretch_ratio = 1.2f,
        layout = new TLayout2
            {
                size = new(200,0),
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
                size_min = new(0, 80),
            },
        };

    public PNL_SceneTree scene_tree = new()
    {
        name = "Outliner",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };
    public PNL_CommonComps common_comps = new()
    {
        name = "Comps",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };
    
    public C2_Inspector inspector_comp=new()
    {
        name = "Component",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };
    public C2_Inspector inspector_scene=new()
    {
        name = "Scene",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };

    ImpScene _bound_scene;
    ImpComp _selected_comp;
    public PNL_FileBrowser file_browser=new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
            size_min = new(0, 200),
        },
    };

    public static WND_Scene active;

    C2_List list_panels;
    C2_Expandable file_browser_wrap;

    public WND_Scene()
    {
        active = this;
        name = "Scene";
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;

        C2_List list_scene_file = new()
        {
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
            orentation = EUIOrentation.V,
        };
        
        C2_List list_main=new()
        {
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
            orentation = EUIOrentation.H,
        };
        Child_Add(list_main);

        tab_scenes.show_close_tab_button = true;
        tab_scenes.request_close_tab = Scene_Close;
        
        list_panels=new()
        {
            layout = new TLayout2
            {
                size = new(300,0),
                size_min = new(180,0),
                orient_V = EUIViewportAlignment.Fill,
            },
            orentation = EUIOrentation.V,
        };
        list_panels.Child_Add(tab_outliners);
        list_panels.Child_Add(new C2_Seperator { orentation = EUIOrentation.V });
        list_panels.Child_Add(tab_inspectors);

        scene_tree.on_item_drop = OnTreeDrop;
        inspector_comp.on_hierarchy_drop = OnHierarchyDrop;
        inspector_comp.on_component_click = OnInspectorComponent;
        scene_tree.on_comp_click = comp =>
        {
            _selected_comp = comp;
            inspector_comp.Objects_Add(new List<object> { comp }, true);
            tab_inspectors.selected_tab = 0;
            PNL_SceneView pnl = ActiveEdScene();
            if (pnl == null) return;
            pnl.gizmo_data.on_selection_changed = null;
            pnl.gizmo_data.Selection_Set(new[] { comp });
            pnl.gizmo_data.on_selection_changed = OnGizmoSelection;
            if (comp is Imp2D) pnl.edit_mode = ESceneEditorMode.Mode_2D;
            else if (comp is Imp3D) pnl.edit_mode = ESceneEditorMode.Mode_3D;
        };
        tab_outliners.tab_width = 88;
        tab_outliners.Child_Add(scene_tree);
        tab_outliners.Child_Add(common_comps);
        
        tab_inspectors.Child_Add(inspector_comp);
        tab_inspectors.Child_Add(inspector_scene);
        tab_inspectors.selected_tab = 1;
        
        file_browser_wrap = new()
        {
            name = "File Browser",
            is_expanded = true,
            bar_height = 22,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
                size_min = new(0, 22),
            },
            stretch_ratio = 0.5f,
        };
        file_browser_wrap.Child_Add(file_browser);

        list_scene_file.Child_Add(tab_scenes);
        list_scene_file.Child_Add(new C2_Seperator { orentation = EUIOrentation.V });
        list_scene_file.Child_Add(file_browser_wrap);
        
        list_main.Child_Add(list_scene_file);
        list_main.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
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
            layout = new TLayout2
            {
                size = new(320, 140),
            },
            pivot = new Vector2(0.5f, 0.5f),
            normalize_pivot = true,
        };
        panel.Position_Set(new Vector2(80, 80));
        C2_Text title = new()
        {
            name = "Title",
            text = "Hello 2D",
            layout = new TLayout2
            {
                size = new(320, 36),
            },
            style = UI_Text.LIGHT,
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

        // Claimed here rather than in PNL_SceneView, because the hotkeys below edit the camera and run
        // before the tab itself updates - they have to land in the open camera's own history.
        PNL_SceneView activePnl = ActiveEdScene();
        if (activePnl != null && IsVisibleInTree())
        {
            activePnl.undo.asset = activePnl.scene;
            ImpUndo.active = activePnl.undo;
        }

        ImpScene scene = activePnl?.scene;
        TabNames_Sync();
        if (scene != _bound_scene)
        {
            BindScene(scene);
            return;
        }
        if (scene == null) return;
        HandleEditHotkeys();
    }

    void HandleEditHotkeys()
    {
        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];
        if (!is_visible) return;
        // Delete / Ctrl+D act on the authored scene's selection. While the game holds input these
        // are the game's keys — otherwise a game bound to Delete destroys real comps mid-play.
        if (!ImpPlayer.TargetGame_IsHost()) return;
        if (player.target_focus is C2_TextEdit te && te.is_focused) return;
        for (ImpComp n = player.target_focus; n != null; n = n.parent)
        {
            if (n == file_browser)
            {
                return;
            }
        }

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
        if (a.IsPackedForeign || b.IsPackedForeign || a.IsOwned || b.IsOwned) return;
        if (where == ETreeDrop.Child && b.IsInstanceRoot) return;

        TCompPlace from = ImpUndo.Place_Get(a);

        if (where == ETreeDrop.Child || b.parent == null)
        {
            a.Reparent(b);
            scene_tree.Expand(b);
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
        PNL_SceneView pnl = ActiveEdScene();
        if (pnl?.scene?.root == null) return;
        List<ImpComp> src = SelectionRoots(pnl.gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(pnl.gizmo_data.selected_comps);
        List<ImpComp> copies = new();
        ImpUndo.Group_Begin("Duplicate");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == pnl.scene.root || s.parent == null || s.IsPackedForeign || s.IsOwned) continue;
            ImpComp copy = s.Clone();
            if (copy == null) continue;
            // Named before the insert below, so the scan never sees the copy itself. The source
            // is still in there under its own name, so the desired name always collides and
            // always picks up a number: mesh -> mesh1 -> mesh2.
            copy.name = ImpComp.Name_Unique(s.parent, string.IsNullOrEmpty(s.name) ? copy.GetType().Name : s.name);
            int idx = s.parent.children.IndexOf(s);
            s.parent.Child_Insert(idx + 1, copy);
            if (copy is Imp3D c3) c3.Position_Set(c3.Position_Get(false) + new Vector3(0.5f, 0f, 0f), false);
            if (copy is Imp2D c2) c2.Position_Set(c2.Position_Get(false) + new Vector2(16f, 16f), false);
            copies.Add(copy);
            // Nothing to come back from - undo takes the copy back out of the camera.
            ImpUndo.Comp_Moved(copy, default, "Duplicate");
        }
        if (copies.Count > 0)
        {
            SelectionUndo(pnl, was_selected, copies);
            pnl.gizmo_data.Selection_Set(copies);
        }
        ImpUndo.Group_End();
    }

    void DeleteSelected()
    {
        PNL_SceneView pnl = ActiveEdScene();
        if (pnl?.scene?.root == null) return;
        List<ImpComp> src = SelectionRoots(pnl.gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(pnl.gizmo_data.selected_comps);
        ImpUndo.Group_Begin("Delete");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == pnl.scene.root || s.IsPackedForeign || s.IsOwned) continue;
            // Detach rather than Destroy: Destroy tears the subtree apart child by child, and
            // undo needs the comp to come back with everything under it still attached.
            TCompPlace from = ImpUndo.Place_Get(s);
            s.Detach();
            ImpUndo.Comp_Moved(s, from, "Delete");
        }
        SelectionUndo(pnl, was_selected, new List<ImpComp>());
        pnl.gizmo_data.Selection_Clear();
        ImpUndo.Group_End();
    }

    // Selection is part of the edit: undoing a delete should hand back what was deleted, and
    // undoing a duplicate must not leave the gizmo driving comps that are no longer in the camera.
    static void SelectionUndo(PNL_SceneView pnl, List<ImpComp> before, List<ImpComp> after)
    {
        ImpUndo.Push("Selection",
            () => pnl.gizmo_data.Selection_Set(before),
            () => pnl.gizmo_data.Selection_Set(after));
    }
    
    // -------------------------------------------------------------------
    // Scene
    // -------------------------------------------------------------------
    public PNL_SceneView Scene_Add(ImpScene scene)
    {
        if (scene == null) return null;
        int page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] == tab_scenes.list_tabs) continue;
            if (tab_scenes.children[i] is PNL_SceneView ed && SameScene(ed.scene, scene))
            {
                tab_scenes.selected_tab = page;
                return ed;
            }
            page++;
        }

        PNL_SceneView uiSceneView = new()
        {
            name = scene.GetName(),
            scene = scene,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
        };
        uiSceneView.viewport3D.view_scene = scene;
        uiSceneView.viewport2D.view_scene = scene;
        uiSceneView.gizmo_data.on_selection_changed = OnGizmoSelection;
        tab_scenes.Child_Add(uiSceneView);
        tab_scenes.selected_tab = page;
        BindScene(scene);
        return uiSceneView;
    }

    public void Scene_Close(int page)
    {
        int i_page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            ImpComp c = tab_scenes.children[i];
            if (c == tab_scenes.list_tabs)
            {
                continue;
            }
            if (i_page != page)
            {
                i_page++;
                continue;
            }

            bool was_sel = tab_scenes.selected_tab == page;
            bool before = page < tab_scenes.selected_tab;
            if (Scene_Editor.active != null && Scene_Editor.active.view_game != null && Scene_Editor.active.view_game.IsDescendantOf(c))
            {
                Scene_Editor.active.MOpt_Play_Stop();
            }
            c.Destroy();
            if (before)
            {
                tab_scenes.selected_tab--;
            }
            else if (was_sel)
            {
                int n = 0;
                for (int k = 0; k < tab_scenes.children.Count; k++)
                {
                    if (tab_scenes.children[k] == tab_scenes.list_tabs)
                    {
                        continue;
                    }
                    n++;
                }
                if (tab_scenes.selected_tab >= n)
                {
                    tab_scenes.selected_tab = Math.Max(0, n - 1);
                }
            }
            BindScene(ActiveEdScene()?.scene);
            return;
        }
    }

    public void Scene_CloseAll()
    {
        if (Scene_Editor.active != null && Scene_Editor.active.view_game != null && Scene_Editor.active.view_game.parent != null)
        {
            Scene_Editor.active.MOpt_Play_Stop();
        }
        for (int i = tab_scenes.children.Count - 1; i >= 0; i--)
        {
            ImpComp c = tab_scenes.children[i];
            if (c == tab_scenes.list_tabs)
            {
                continue;
            }
            c.Destroy();
        }
        tab_scenes.selected_tab = 0;
        BindScene(null);
    }

    public void State_Capture(EdStateData data)
    {
        if (data == null)
        {
            return;
        }
        data.inspector_tab = tab_inspectors.selected_tab;
        data.outliner_tab = tab_outliners.selected_tab;
        data.file_browser_expanded = file_browser_wrap == null || file_browser_wrap.is_expanded;
        if (list_panels != null)
        {
            data.panel_width = list_panels.layout.size.X;
        }
        if (file_browser_wrap != null)
        {
            data.browser_stretch = file_browser_wrap.stretch_ratio;
        }
        data.scene_tabs_stretch = tab_scenes.stretch_ratio;
        data.outliner_stretch = tab_outliners.stretch_ratio;
        data.inspector_stretch = tab_inspectors.stretch_ratio;
        data.active_scene = 0;
        data.scenes.Clear();

        int page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] == tab_scenes.list_tabs)
            {
                continue;
            }
            if (tab_scenes.children[i] is not PNL_SceneView ed || ed.scene == null)
            {
                continue;
            }
            if (page == tab_scenes.selected_tab)
            {
                data.active_scene = data.scenes.Count;
            }
            page++;

            string path = ed.scene.filepath;
            if (string.IsNullOrWhiteSpace(path) || ImpAsset.Path_IsBuiltin(path))
            {
                continue;
            }
            string disk = EdState.Path_Load(path);
            if (!ed.scene.File_CanWrite() && !ed.scene.File_IsValid() && !File.Exists(disk))
            {
                continue;
            }
            EdStateScene s = new();
            s.path = EdState.Path_Store(path);
            s.edit_mode = ed.edit_mode.ToString();
            s.gizmo_mode = ed.gizmo_mode.ToString();
            s.gizmo_space = ed.gizmo_orientation.ToString();
            s.snap_translate = ed.gizmo_data.snap_translate;
            s.snap_translate_2d = ed.gizmo_data.snap_translate_2d;
            Camera3D cam = ed.viewport3D.camera;
            s.cam3_position = new[] { cam.Position.X, cam.Position.Y, cam.Position.Z };
            s.cam3_target = new[] { cam.Target.X, cam.Target.Y, cam.Target.Z };
            s.cam3_fovy = cam.FovY;
            TCamera2D cam2 = ed.viewport2D.camera;
            s.cam2_position = new[] { cam2.position.X, cam2.position.Y };
            s.cam2_zoom = cam2.zoom;
            s.selected = SelectionPaths(ed);
            data.scenes.Add(s);
        }
    }

    public void State_Apply(EdStateData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.panel_width >= 180 && list_panels != null)
        {
            list_panels.layout.size = new Vector2(data.panel_width, list_panels.layout.size.Y);
        }
        if (file_browser_wrap != null)
        {
            file_browser_wrap.is_expanded = data.file_browser_expanded;
            if (EdState.Stretch_IsWeight(data.browser_stretch))
            {
                file_browser_wrap.stretch_ratio = data.browser_stretch;
            }
        }
        if (EdState.Stretch_IsWeight(data.scene_tabs_stretch))
        {
            tab_scenes.stretch_ratio = data.scene_tabs_stretch;
        }
        if (data.outliner_stretch > 0)
        {
            tab_outliners.stretch_ratio = data.outliner_stretch;
        }
        if (data.inspector_stretch > 0)
        {
            tab_inspectors.stretch_ratio = data.inspector_stretch;
        }
        tab_outliners.selected_tab = data.outliner_tab;
        tab_inspectors.selected_tab = data.inspector_tab;

        if (data.scenes.Count == 0)
        {
            return;
        }

        List<(ImpScene scene, EdStateScene state)> loaded = new();
        for (int i = 0; i < data.scenes.Count; i++)
        {
            EdStateScene s = data.scenes[i];
            ImpScene scene = LoadScene(s.path);
            if (scene == null)
            {
                continue;
            }
            loaded.Add((scene, s));
        }
        if (loaded.Count == 0)
        {
            return;
        }

        Scene_CloseAll();
        for (int i = 0; i < loaded.Count; i++)
        {
            EdStateScene s = loaded[i].state;
            PNL_SceneView pnl = Scene_Add(loaded[i].scene);
            if (pnl == null)
            {
                continue;
            }
            if (Enum.TryParse(s.edit_mode, out ESceneEditorMode mode))
            {
                pnl.edit_mode = mode;
            }
            if (Enum.TryParse(s.gizmo_mode, out EGizmoMode giz))
            {
                pnl.gizmo_mode = giz;
            }
            if (Enum.TryParse(s.gizmo_space, out EGizmoSpace space))
            {
                pnl.gizmo_orientation = space;
            }
            if (s.snap_translate > 0)
            {
                pnl.gizmo_data.snap_translate = s.snap_translate;
            }
            if (s.snap_translate_2d > 0)
            {
                pnl.gizmo_data.snap_translate_2d = s.snap_translate_2d;
            }
            pnl.Camera3_Apply(Vec3(s.cam3_position, new Vector3(6.5f, 4.5f, 8.5f)), Vec3(s.cam3_target, new Vector3(0f, 0.5f, 0f)), s.cam3_fovy);
            pnl.Camera2_Apply(Vec2(s.cam2_position, new Vector2(960f, 540f)), s.cam2_zoom);
            ApplySelection(pnl, s.selected);
        }

        if (data.active_scene >= 0)
        {
            tab_scenes.selected_tab = data.active_scene;
        }
        BindScene(ActiveEdScene()?.scene);
    }

    static ImpScene LoadScene(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }
        string path = EdState.Path_Load(stored);
        if (!string.IsNullOrEmpty(path))
        {
            ImpAsset a = ImpAsset.Load(path);
            if (a is ImpScene s)
            {
                return s;
            }
        }
        if (!string.Equals(path, stored, StringComparison.OrdinalIgnoreCase))
        {
            ImpAsset a = ImpAsset.Load(stored);
            if (a is ImpScene s)
            {
                return s;
            }
        }
        return null;
    }

    static string[] SelectionPaths(PNL_SceneView pnl)
    {
        if (pnl?.scene?.root == null || pnl.gizmo_data.selected_comps.Count == 0)
        {
            return Array.Empty<string>();
        }
        List<string> paths = new();
        ImpComp root = pnl.scene.root;
        for (int i = 0; i < pnl.gizmo_data.selected_comps.Count; i++)
        {
            ImpComp c = pnl.gizmo_data.selected_comps[i];
            if (c == null || c == root)
            {
                continue;
            }
            string p = CompPath(c, root);
            if (!string.IsNullOrEmpty(p))
            {
                paths.Add(p);
            }
        }
        return paths.ToArray();
    }

    static void ApplySelection(PNL_SceneView pnl, string[] paths)
    {
        if (pnl?.scene?.root == null || paths == null || paths.Length == 0)
        {
            return;
        }
        List<ImpComp> comps = new();
        for (int i = 0; i < paths.Length; i++)
        {
            ImpComp c = CompFind(pnl.scene.root, paths[i]);
            if (c != null)
            {
                comps.Add(c);
            }
        }
        if (comps.Count > 0)
        {
            pnl.gizmo_data.Selection_Set(comps);
        }
    }

    static string CompPath(ImpComp c, ImpComp root)
    {
        List<string> parts = new();
        ImpComp n = c;
        while (n != null && n != root)
        {
            parts.Add(n.name ?? "");
            n = n.parent;
        }
        if (n != root)
        {
            return "";
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static ImpComp CompFind(ImpComp root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
        {
            return null;
        }
        string[] parts = path.Split('/');
        ImpComp cur = root;
        for (int i = 0; i < parts.Length; i++)
        {
            ImpComp next = null;
            for (int k = 0; k < cur.children.Count; k++)
            {
                if (string.Equals(cur.children[k].name, parts[i], StringComparison.Ordinal))
                {
                    next = cur.children[k];
                    break;
                }
            }
            if (next == null)
            {
                return null;
            }
            cur = next;
        }
        return cur;
    }

    static Vector3 Vec3(float[] v, Vector3 fallback)
    {
        if (v == null || v.Length < 3)
        {
            return fallback;
        }
        return new Vector3(v[0], v[1], v[2]);
    }

    static Vector2 Vec2(float[] v, Vector2 fallback)
    {
        if (v == null || v.Length < 2)
        {
            return fallback;
        }
        return new Vector2(v[0], v[1]);
    }

    void OnInspectorComponent(ImpComp comp)
    {
        if (comp == null)
        {
            return;
        }
        _selected_comp = comp;
        PNL_SceneView pnl = ActiveEdScene();
        if (pnl == null)
        {
            return;
        }
        pnl.gizmo_data.on_selection_changed = null;
        pnl.gizmo_data.Selection_Set(new[] { comp });
        pnl.gizmo_data.on_selection_changed = OnGizmoSelection;
        ImpComp host = ImpComp.OutlinerHost(comp);
        if (host == null)
        {
            host = comp;
        }
        scene_tree.Select(host);
        if (comp is Imp2D)
        {
            pnl.edit_mode = ESceneEditorMode.Mode_2D;
        }
        else if (comp is Imp3D)
        {
            pnl.edit_mode = ESceneEditorMode.Mode_3D;
        }
    }

    void OnGizmoSelection()
    {
        PNL_SceneView pnl = ActiveEdScene();
        if (pnl == null) return;
        List<ImpComp> sel = pnl.gizmo_data.selected_comps;
        _selected_comp = sel.Count > 0 ? sel[0] : null;
        if (_selected_comp != null)
        {
            List<object> objs = new();
            for (int i = 0; i < sel.Count; i++) objs.Add(sel[i]);
            inspector_comp.Objects_Add(objs, true);
            tab_inspectors.selected_tab = 0;
            ImpComp host = ImpComp.OutlinerHost(_selected_comp);
            if (host == null)
            {
                host = _selected_comp;
            }
            scene_tree.Select(host);
        }
        else
        {
            inspector_comp.Objects_Clear();
            scene_tree.Select(null);
        }
    }

    public PNL_SceneView ActiveEdScene()
    {
        int page = 0;
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] == tab_scenes.list_tabs) continue;
            if (tab_scenes.children[i] is not PNL_SceneView ed) continue;
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
        scene_tree.scene = scene;
        scene_tree.Refresh(true);
        PNL_SceneView pnl = ActiveEdScene();
        if (pnl != null)
        {
            pnl.gizmo_data.on_selection_changed = OnGizmoSelection;
            OnGizmoSelection();
        }
    }

    static bool SameScene(ImpScene a, ImpScene b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        if (string.IsNullOrEmpty(a.filepath) || string.IsNullOrEmpty(b.filepath)) return false;
        try { return string.Equals(Path.GetFullPath(a.filepath), Path.GetFullPath(b.filepath), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a.filepath, b.filepath, StringComparison.OrdinalIgnoreCase); }
    }

    public override void OnTrySave()
    {
        ImpScene scene = ActiveEdScene()?.scene;
        if (scene == null) return;
        if (scene.File_CanWrite())
        {
            scene.File_Write();
            AfterSaved();
            return;
        }
        OnTrySaveAs();
    }

    public override void OnTrySaveAs()
    {
        ImpScene scene = ActiveEdScene()?.scene;
        if (scene == null) return;

        string folder = file_browser.CurrentDir;
        DLG_SaveFile.Run(scene, path =>
        {
            scene.File_SaveTo(path);
            AfterSaved();
        }, folder);
    }

    void AfterSaved()
    {
        TabNames_Sync();
        PNL_FileBrowser.Browsers_Notify();
    }

    void TabNames_Sync()
    {
        for (int i = 0; i < tab_scenes.children.Count; i++)
        {
            if (tab_scenes.children[i] is not PNL_SceneView ed) continue;
            if (ed.scene == null) continue;
            string n = ed.scene.GetName();
            if (ed.scene.is_dirty && ed.scene.File_IsValid() && !n.EndsWith("*"))
            {
                n += "*";
            }
            if (ed.name != n)
            {
                ed.name = n;
            }
        }
    }
}