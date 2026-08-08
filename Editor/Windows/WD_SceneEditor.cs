using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Windows;

// Scene editing tab: open scenes fill the left, a fixed-width panel column sits on the
// right holding the outliner over the inspectors.
//
//   c_list_main (horizontal)
//     c_tabs_scenes   -> one C2_SceneView per open scene, expands to take the leftover
//     c_list_panels   -> fixed width column
//       c_tabs_panel_top    -> Outliner
//       c_tabs_panel_bottom -> Details / Scene inspectors
public class WD_SceneEditor : EdWindow
{
    //width of the right-hand panel column; the viewport takes whatever's left
    public const float panel_width = 320f;

    // Tab boxes place children by anchor, not by exact rect, so every panel needs Full or
    // it resolves against its content-min size and collapses to nothing.
    public C2_Inspector c_comp_inspector = new C2_Inspector
    {
        name = "Details",
        anchor_preset = EUIAnchorPreset.Full,
    };
    public C2_Inspector c_scene_inspector = new C2_Inspector
    {
        name = "Scene",
        anchor_preset = EUIAnchorPreset.Full,
    };
    public C2_Tree c_scene_tree = new C2_Tree
    {
        name = "Outliner",
        anchor_preset = EUIAnchorPreset.Full,
    };

    // Both panel tab boxes expand vertically with the same ratio, so the column splits
    // evenly between them.
    public C2_TabBox c_tabs_panel_top = new C2_TabBox
    {
        name = "PanelTop",
        sizing_vertical = new TUISizing { preset = EUISizingPreset.Fill, expand = true },
    };
    public C2_TabBox c_tabs_panel_bottom = new C2_TabBox
    {
        name = "PanelBottom",
        sizing_vertical = new TUISizing { preset = EUISizingPreset.Fill, expand = true },
    };

    // No explicit width: expand takes everything the panel column doesn't claim.
    public C2_TabBox c_tabs_scenes = new C2_TabBox
    {
        name = "Scenes",
        sizing_horizontal = new TUISizing { preset = EUISizingPreset.Fill, expand = true },
        
    };

    public List<C2_SceneView> c_open_scenes = new List<C2_SceneView>();

    public C2_List c_list_main = new C2_List
    {
        name = "Main",
        anchor_preset = EUIAnchorPreset.Full,
        Alignment = EUIAlignment.Horizontal,
        cursor_filter = ECursorFilter.Pass, //pure layout container
    }; // main layout

    // size.X is the main axis for this column's parent list, so it fixes the width;
    // the cross axis (height) fills, per the default Fill sizing preset.
    public C2_List c_list_panels = new C2_List
    {
        name = "Panels",
        Alignment = EUIAlignment.Vertical,
        size = new Vector2(panel_width, 0),
        cursor_filter = ECursorFilter.Pass,
    };

    public WD_SceneEditor()
    {
        name = "Scene";
        cursor_filter = ECursorFilter.Pass;

        // picking in the outliner drives the Details inspector; with nothing left selected
        // it falls back to the scene itself rather than an empty panel
        c_scene_tree.on_select = (C2_Tree tree, TTreeItem item) =>
        {
            if (item.data is ImpComp comp) c_comp_inspector.Select(comp);
        };
        c_scene_tree.on_deselect = (C2_Tree tree, TTreeItem item) =>
        {
            if (tree.selected.Count == 0) c_comp_inspector.Select_Clear();
        };

        c_tabs_panel_top.Child_Add(c_scene_tree);

        c_tabs_panel_bottom.Child_Add(c_comp_inspector);
        c_tabs_panel_bottom.Child_Add(c_scene_inspector);

        c_list_panels.Child_Add(c_tabs_panel_top);
        c_list_panels.Child_Add(c_tabs_panel_bottom);

        c_tabs_scenes.on_tab_changed = (C2_TabBox comp, int index) =>
        {
            C2_SceneView? _comp = c_open_scenes[index];
            Scene_Select(_comp);
        };
        c_tabs_scenes.on_request_close_tab = (C2_TabBox comp, int index, ImpComp tab) =>
        {
            Scene_Close(c_open_scenes[index].scene);
        };
        
        c_list_main.Child_Add(c_tabs_scenes);
        c_list_main.Child_Add(c_list_panels);

        Child_Add(c_list_main);

        // open default scene. this should be replaced later with auto-opening last open scenes
        Scene_Open(Scene_CreateDefault());
    }

    // ------------------------------------------------------------
    // SCENE
    // ------------------------------------------------------------

    // Starting scene for a fresh session: a lit cube at the origin, so the viewport has
    // something to show before any level loading exists.
    static ImpScene Scene_CreateDefault()
    {
        var scene = new ImpScene();
        scene.root.name = "Scene";

        scene.root.Child_Add(new C3_Mesh
        {
            name = "Cube",
            mesh = A_Mesh.Primitive(EMeshPrimitive.Cube),
            materials = [new A_Material { albedo_color = new Color(196, 200, 210, 255), roughness = 0.55f }],
        });

        scene.root.Child_Add(new C3_Light
        {
            name = "Sun",
            transform = new TTransform3 { rotation = new Vector3(-45f, -35f, 0f) },
            energy = 2.0f,
        });

        return scene;
    }

    public void Scene_Open(ImpScene scene)
    {
        C2_SceneView new_ui = new C2_SceneView();
        new_ui.scene = scene;
        new_ui.name = Scene_Name(scene);
        new_ui.anchor_preset = EUIAnchorPreset.Full;
        new_ui.input_enabled = true;
        new_ui.is_editor=true;
        new_ui.is_debug_view=true;

        c_open_scenes.Add(new_ui);
        c_tabs_scenes.Child_Add(new_ui);

        // opening a scene points the scene inspector at it. Rows are built against the
        // editor theme because the inspector is already parented by this point.
        Scene_Select(new_ui);
    }

    public void Scene_Close(ImpScene scene)
    {
        C2_SceneView? _comp = Scene_GetComp(scene);
        if (_comp == null) return;

        c_open_scenes.Remove(_comp);
        _comp.Destroy();
    }

    public C2_SceneView? Scene_GetComp(ImpScene scene)
    {
        foreach (var comp in c_open_scenes)
        {
            if (comp.scene==scene)
            {
                return comp;
            }
        }
        return null;
    }

    public void Scene_Select(C2_SceneView comp)
    {
        c_scene_inspector.Select(comp.scene);
        c_scene_tree.Build_FromComp(comp.scene.root);
    }

    // Tab label for a scene. ImpScene has no name of its own yet, so this falls back to
    // the root comp's name and then to a numbered default.
    string Scene_Name(ImpScene scene)
    {
        if (!string.IsNullOrEmpty(scene.root?.name)) return scene.root.name;
        return $"Scene {c_open_scenes.Count + 1}";
    }
}
