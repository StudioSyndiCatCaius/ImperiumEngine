using System.Numerics;
using Editor.Windows;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Scenes;

public class Scene_Editor : ImpComp
{
    public C2_Box ui_main = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };

    public C2_List ui_main_buttons = new()
    {
        alignment = EUIAlignment.Horizontal,
        is_scrollable = false,
        view_alighnment_H = EUIViewportAlignment.Fill,
       // override_child_alignment_h = true,
       // child_alignment_h = EUIViewportAlignment.Fill,
        size = new Vector2(0, 55),
        //size_min = new Vector2(0, 28),
    };

    // stacks menubar then content so tabs don't cover the bar hit rect
    public C2_List ui_column = new()
    {
        alignment = EUIAlignment.Vertical,
        is_scrollable = false,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };

    public C2_MenuBar ui_menubar = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        size = new Vector2(0, 28),
        size_min = new Vector2(0, 28),
    };

    public C2_TabBox ui_main_tabs = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        size = new Vector2(0, 800),
        size_min = new Vector2(0, 100),
    };
    
    //the Edit menu's own suboptions, kept so Undo/Redo can show what they would undo
    List<TMenuBarSubption> _edit_options;

    // main tabs
    public WND_Scene mtab_scene = new();
    public WND_Asset mtab_asset = new();
    public WND_ConfigGame mtab_config_game = new();
    public WND_ConfigEditor mtab_config_editor = new();
    

    public Scene_Editor()
    {
        ui_menubar.options = new()
        {
            new()
            {
                text = "File",
                suboptions =
                {
                    new() { text = "New Scene", hotkey_key=EInputKey.Key_N, on_press = () => { MOpt_New_Scene(); } },
                    new() { text = "New Asset", on_press = () => { MOpt_New_Asset(); } },
                    new() { text = "Save", hotkey_key=EInputKey.Key_S, on_press = () => { MOpt_Save(); } },
                    new() { text = "Save As", on_press = () => { MOpt_Save_As(); } },
                    new() { text = "Save All", on_press = () => { MOpt_Save_All(); } },
                },
            },
            new()
            {
                text = "Edit",
                suboptions =
                {
                    new() { text = "Undo", hotkey_key=EInputKey.Key_Z, on_press = () => { MOpt_Undo(); } },
                    new() { text = "Redo", hotkey_key=EInputKey.Key_Z, hotkey_require_shift=true, on_press = () => { MOpt_Redo(); } },
                },
            },
        };
        _edit_options = ui_menubar.options[1].suboptions;
        
        // ---------------------
        // Main Buttons
        // ---------------------

        void _AddMainButton(string text, A_Texture icon, Action on_press)
        {
            C2_Button _btn = new()
            {
                text = text,
                icon = icon,
                on_click = on_press,
                layout = EButtonLayout.Icon_Text_V,
                text_style = UiStyle_Text.DEFAULT,
                icon_size = 28,
                override_font_size = 10,
                size_min = new Vector2(80, 0),
                //view_alighnment_H = EUIViewportAlignment.Fill,
                view_alighnment_V = EUIViewportAlignment.Fill,
                stretch_ratio = 1f,
            };
            ui_main_buttons.Child_Add(_btn);
        }

        void _AddMainSeperator()
        {
            C2_Seperator _sep = new()
            {
                alignment = EUIAlignment.Horizontal,
                is_draggable = false,
                thickness = 8,
                size = new Vector2(8, 55),
                view_alighnment_V = EUIViewportAlignment.Fill,
            };
            ui_main_buttons.Child_Add(_sep);
        }
        
        _AddMainButton("New Scene", A_Texture.ICO_SCENE, () => { MOpt_New_Scene(); });
        _AddMainButton("New Asset", A_Texture.ICO_SCENE, () => { MOpt_New_Asset(); });
        _AddMainSeperator();
        _AddMainButton("Save", A_Texture.ICO_SAVE, () => { MOpt_Save(); });
        _AddMainButton("Save As", A_Texture.ICO_SAVE, () => { MOpt_Save_As(); });
        _AddMainButton("Save All", A_Texture.ICO_SAVE, () => { MOpt_Save_All(); });
        _AddMainSeperator();
        _AddMainButton("Play", A_Texture.ICO_PLAY, () => { MOpt_Play(); });
        _AddMainButton("Play From Start", A_Texture.ICO_PLAY, () => { MOpt_Play_Start(); });

        // ---------------------
        // Main Tabs
        // ---------------------
        ui_main_tabs.Child_Add(mtab_scene);
        ui_main_tabs.Child_Add(mtab_asset);
        ui_main_tabs.Child_Add(mtab_config_game);
        ui_main_tabs.Child_Add(mtab_config_editor);
        
        // ---------------------
        // FINALIZE & ASSEMBLY
        // ---------------------
        
        ui_column.Child_Add(ui_menubar);
        ui_column.Child_Add(ui_main_buttons);
        ui_column.Child_Add(ui_main_tabs);
        ui_main.Child_Add(ui_column);
        Child_Add(ui_main);

        ImpAsset.Editor_OnOpenAsset = asset =>
        {
            if (asset is ImpScene scene)
            {
                mtab_scene.Scene_Add(scene);
                ui_main_tabs.selected_tab = 0;
            }
            else
            {
                mtab_asset.Asset_Add(asset);
                ui_main_tabs.selected_tab = 1;
            }
        };
    }
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        // Ctrl+Y is the other half of the Windows convention; Ctrl+Z / Ctrl+Shift+Z come off
        // the menu entries themselves.
        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        if (ctrl && ImpPlayer.Key_IsPressed(EInputKey.Key_Y)) MOpt_Redo();

        if (_edit_options == null || _edit_options.Count < 2) return;
        ImpUndo undo = ImpUndo.active;
        _Sync(0, "Undo", undo != null && undo.CanUndo, undo?.UndoLabel);
        _Sync(1, "Redo", undo != null && undo.CanRedo, undo?.RedoLabel);

        void _Sync(int i, string verb, bool can, string what)
        {
            string text = can && !string.IsNullOrEmpty(what) ? verb + " " + what : verb;
            TMenuBarSubption opt = _edit_options[i];
            if (opt.text == text && opt.is_disabled == !can) return;
            opt.text = text;
            opt.is_disabled = !can;
            _edit_options[i] = opt;
        }
    }

    // -------------------------------------------------------------------
    // Main Options
    // -------------------------------------------------------------------

    public void MOpt_Undo()
    {
        if (Text_IsFocused()) return;
        ImpUndo.active?.Undo();
    }

    public void MOpt_Redo()
    {
        if (Text_IsFocused()) return;
        ImpUndo.active?.Redo();
    }

    //while a field has the caret, Ctrl+Z belongs to that field, not to the scene
    static bool Text_IsFocused()
    {
        if (ImpPlayer.players.Count == 0) return false;
        return ImpPlayer.players[0].ui_focus is C2_TextEdit { is_focused: true };
    }

    public void MOpt_New_Scene()
    { 
        Console.WriteLine("New Scene");   
    }
    public void MOpt_New_Asset()
    {
        Console.WriteLine("New Asset");  
    }
    
    public void MOpt_Save()
    {
        Console.WriteLine("Save"); 
    }
    
    public void MOpt_Save_As()
    {
        Console.WriteLine("Save As");
    }
    
    public void MOpt_Save_All()
    {
        ImpAsset.SaveAllDirty();
    }
    
    public void MOpt_Play()
    {
        Console.WriteLine("Play");
    }

    public void MOpt_Play_Start()
    {
        Console.WriteLine("Play from Start");
    }

    
}