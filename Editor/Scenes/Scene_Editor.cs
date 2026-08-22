using System.Diagnostics;
using System.Numerics;
using Editor.Dialog;
using Editor.Panel;
using Editor.Windows;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Enums;
using ImperiumEngine.Files;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Scenes;

public enum EPlayMode
{
    PlayInEditor,
    Standalone,
}

public class Scene_Editor : ImpComp
{
    public static Scene_Editor active;

    public C2_Box ui_main = new()
    {
        layout = TLayout2.FULL
    };

    // tall enough for icon + label underneath, or the buttons spill over the menubar
    public const float MAIN_BUTTON_ICON = 20;
    public const float MAIN_BUTTON_HEIGHT = 36;
    public const float MAIN_BUTTON_WIDTH = 80;

    public C2_List ui_main_buttons = new()
    {
        orentation = EUIOrentation.H,
        is_scrollable = false,
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, MAIN_BUTTON_HEIGHT),
        },
    };

    // stacks menubar then content so tabs don't cover the bar hit rect
    public C2_List ui_column = new()
    {
        orentation = EUIOrentation.V,
        is_scrollable = false,
        layout = TLayout2.FULL
    };

    public C2_MenuBar ui_menubar = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, 24),
        },
        dropdown_width = 240,
    };

    // splits the content row into the docked browser and everything else
    public C2_List ui_body = new()
    {
        orentation = EUIOrentation.H,
        is_scrollable = false,
        layout = TLayout2.FULL
    };

    // One browser for the whole editor, docked left rather than repeated per tab, so every
    // window is looking at the same folder.
    public PNL_FileBrowser file_browser = new()
    {
        layout = new TLayout2
        {
            orient_V = EUIViewportAlignment.Fill,
            size = new Vector2(280, 0),
            size_min = new Vector2(180, 0),
        },
    };

    public C2_TabBox ui_main_tabs = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
            size = new Vector2(0, 800),
            size_min = new Vector2(300, 100),
        },
        tab_width = 120,
    };

    //the Edit menu's own suboptions, kept so Undo/Redo can show what they would undo
    List<TMenuBarSubption> _edit_options;

    // main tabs
    public WND_Scene mtab_scene = new();
    public WND_Asset mtab_asset = new();
    public WND_Flow mtab_flow = new();
    public WND_ConfigComp mtab_config_comp = new();
    public WND_ConfigGame mtab_config_game = new();
    public WND_ConfigEditor mtab_config_editor = new();

    public C2_Button btn_stop;
    public C2_Button btn_play;
    public C2_Button btn_play_start;
    public C2_Dropdown drop_play_mode;
    public EPlayMode play_mode = EPlayMode.PlayInEditor;
    Process? _standalone;
    
    public Scene_Editor()
    {
        active = this;
        ui_menubar.options = new()
        {
            new()
            {
                text = "File",
                suboptions =
                {
                    new() { text = "New Scene", hotkey_key=EInputKey.Key_N, on_press = () => { MOpt_New_Scene(); } },
                    new() { text = "New Asset", on_press = () => { MOpt_New_Asset(); } },
                    new() { text = "Save    Ctrl+S", hotkey_key=EInputKey.Key_S, on_press = () => { MOpt_Save(); } },
                    new() { text = "Save As    Ctrl+Shift+S", hotkey_key=EInputKey.Key_S, hotkey_require_shift=true, on_press = () => { MOpt_Save_As(); } },
                    new() { text = "Save All    Ctrl+Alt+S", hotkey_key=EInputKey.Key_S, hotkey_require_alt=true, on_press = () => { MOpt_Save_All(); } },
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

        C2_Button _AddMainButton(string text, A_Texture icon, Action on_press)
        {
            C2_Button _btn = new()
            {
                text = text,
                icon = icon,
                on_click = on_press,
                //button_layout = EButtonLayout.Icon_Text_V,
                text_style = UI_Text.DEFAULT,
                icon_size = MAIN_BUTTON_ICON,
                override_font_size = 10,
                gap = 2,
                layout = new TLayout2
                {
                    // fixed width; the bar packs left to right and leaves the tail empty
                    size = new Vector2(MAIN_BUTTON_WIDTH, MAIN_BUTTON_HEIGHT),
                    size_min = new Vector2(MAIN_BUTTON_WIDTH, 0),
                    orient_V = EUIViewportAlignment.Fill,
                },
            };
            ui_main_buttons.Child_Add(_btn);
            return _btn;
        }

        void _AddMainSeperator()
        {
            C2_Seperator _sep = new()
            {
                orentation = EUIOrentation.H,
                is_draggable = false,
                thickness = 8,
                layout = new TLayout2
                {
                    size = new Vector2(8, MAIN_BUTTON_HEIGHT),
                    orient_V = EUIViewportAlignment.Fill,
                },
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
        btn_stop = _AddMainButton("Stop", A_Texture.ICO_STOP, () => { MOpt_Play_Stop(); });
        btn_play = _AddMainButton("Play", A_Texture.ICO_PLAY, () => { MOpt_Play(); });
        btn_play_start = _AddMainButton("Play (Start)", A_Texture.ICO_PLAY, () => { MOpt_Play_Start(); });
        btn_stop.is_disabled = true;

        drop_play_mode = new C2_Dropdown
        {
            layout = new TLayout2
            {
                size = new Vector2(160, MAIN_BUTTON_HEIGHT),
                size_min = new Vector2(160, 0),
                orient_V = EUIViewportAlignment.Fill,
            },
        };
        drop_play_mode.Options_Set(new[] { "Play-in-Editor", "Standalone" });
        drop_play_mode.Option_SetQuiet(0);
        drop_play_mode.on_dropdown_change = (_, _, idx) =>
        {
            if (idx == 1)
            {
                play_mode = EPlayMode.Standalone;
            }
            else
            {
                play_mode = EPlayMode.PlayInEditor;
            }
        };
        ui_main_buttons.Child_Add(drop_play_mode);

        // ---------------------
        // Main Tabs
        // ---------------------
        ui_main_tabs.Child_Add(mtab_scene);
        ui_main_tabs.Child_Add(mtab_asset);
        ui_main_tabs.Child_Add(mtab_flow);
        ui_main_tabs.Child_Add(mtab_config_game);
        ui_main_tabs.Child_Add(mtab_config_comp);
        ui_main_tabs.Child_Add(mtab_config_editor);
        
        // ---------------------
        // FINALIZE & ASSEMBLY
        // ---------------------
        
        ui_body.Child_Add(file_browser);
        ui_body.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        ui_body.Child_Add(ui_main_tabs);

        ui_column.Child_Add(ui_menubar);
        ui_column.Child_Add(ui_main_buttons);
        ui_column.Child_Add(ui_body);
        ui_main.Child_Add(ui_column);
        Child_Add(ui_main);

        ImpAsset.Editor_OnOpenAsset = asset =>
        {
            if (asset is ImpScene scene)
            {
                mtab_scene.Scene_Add(scene);
                SelectMainWindow(mtab_scene);
            }
            else if (asset is A_Flow flow)
            {
                mtab_flow.Flow_Add(flow);
                SelectMainWindow(mtab_flow);
            }
            else
            {
                mtab_asset.Asset_Add(asset);
                SelectMainWindow(mtab_asset);
            }
        };

        ImpFile.Editor_OnCreateAssetFromFile = file =>
        {
            DLG_CreateAssetFromFile.Run(file);
        };

        EdState.Load(this);
    }
    
    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        // Ctrl+Y is the other half of the Windows convention; Ctrl+Z / Ctrl+Shift+Z come off
        // the menu entries themselves.
        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        if (ctrl && ImpPlayer.Key_IsPressed(EInputKey.Key_Y)) MOpt_Redo();

        bool pie = ImpGame.Get(ImpGame.ID_PLAY) != null;
        bool standalone = Standalone_IsLive();
        if (btn_stop != null)
        {
            btn_stop.is_disabled = !pie && !standalone;
        }
        if (btn_play != null)
        {
            btn_play.is_disabled = pie;
        }
        if (btn_play_start != null)
        {
            btn_play_start.is_disabled = pie;
        }
        if (!pie && ImpPlayer.Action_IsPressed("PIE_Play"))
        {
            MOpt_Play();
        }
        if (pie && ImpPlayer.Action_IsPressed("PIE_Quit"))
        {
            MOpt_Play_Stop();
        }

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

        EdState.Tick(this, dt);
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

    //while a field has the caret, Ctrl+Z belongs to that field, not to the camera
    static bool Text_IsFocused()
    {
        if (ImpPlayer.players.Count == 0) return false;
        return ImpPlayer.players[0].target_focus is C2_TextEdit { is_focused: true };
    }

    public void MOpt_New_Scene()
    {
        DLG_NewScene.Run(Folder_ForCreate());
    }

    public void MOpt_New_Asset()
    {
        DLG_NewAsset.Run(Folder_ForCreate());
    }

    string Folder_ForCreate()
    {
        return file_browser.CurrentDir;
    }
    
    public void MOpt_Save()
    {
        EdWindow w = ActiveWindow();
        if (w == null) return;
        w.OnTrySave();
    }

    public void MOpt_Save_As()
    {
        EdWindow w = ActiveWindow();
        if (w == null) return;
        w.OnTrySaveAs();
    }

    public void MOpt_Save_All()
    {
        List<ImpAsset> untitled = new();

        void Consider(ImpAsset a)
        {
            if (a == null) return;
            if (a.File_CanWrite())
            {
                if (a.is_dirty)
                {
                    a.File_Write();
                }
                return;
            }
            if (!untitled.Contains(a))
            {
                untitled.Add(a);
            }
        }

        for (int i = 0; i < mtab_scene.tab_scenes.children.Count; i++)
        {
            if (mtab_scene.tab_scenes.children[i] is PNL_SceneView ed && ed.scene != null)
            {
                Consider(ed.scene);
            }
        }
        for (int i = 0; i < mtab_asset.tab_assets.children.Count; i++)
        {
            if (mtab_asset.tab_assets.children[i] is EdAssetEditor ed && ed.asset != null)
            {
                Consider(ed.asset);
            }
        }
        for (int i = 0; i < mtab_flow.tab_flows.children.Count; i++)
        {
            if (mtab_flow.tab_flows.children[i] is PNL_FlowGraph ed && ed.flow != null)
            {
                Consider(ed.flow);
            }
        }
        foreach (ImpAsset a in ImpAsset.Loaded_GetAll())
        {
            Consider(a);
        }

        PNL_FileBrowser.Browsers_Notify();

        void PromptNext()
        {
            if (untitled.Count == 0) return;
            ImpAsset a = untitled[0];
            untitled.RemoveAt(0);
            string folder = file_browser.CurrentDir;
            DLG_SaveFile.Run(a, path =>
            {
                a.File_SaveTo(path);
                PNL_FileBrowser.Browsers_Notify();
                PromptNext();
            }, folder);
        }
        PromptNext();
    }

    EdWindow ActiveWindow()
    {
        int page = 0;
        for (int i = 0; i < ui_main_tabs.children.Count; i++)
        {
            if (ui_main_tabs.children[i] == ui_main_tabs.list_tabs) continue;
            if (ui_main_tabs.children[i] is not EdWindow w) continue;
            if (page == ui_main_tabs.selected_tab) return w;
            page++;
        }
        return null;
    }

    void SelectMainWindow(EdWindow window)
    {
        if (window == null)
        {
            return;
        }
        int page = 0;
        for (int i = 0; i < ui_main_tabs.children.Count; i++)
        {
            if (ui_main_tabs.children[i] == ui_main_tabs.list_tabs)
            {
                continue;
            }
            if (ui_main_tabs.children[i] == window)
            {
                ui_main_tabs.selected_tab = page;
                return;
            }
            page++;
        }
    }
    
    public void MOpt_Play()
    {
        if (play_mode == EPlayMode.Standalone)
        {
            Standalone_Play();
            return;
        }
        if (ImpGame.Get(ImpGame.ID_PLAY) != null)
        {
            return;
        }
        PNL_SceneView view = mtab_scene.ActiveEdScene();
        if (view == null || view.scene == null || view.game_view == null)
        {
            return;
        }
        ImpGame game = ImpGame.Play_Start(view.scene);
        if (game == null)
        {
            return;
        }
        view.game_view.Play(game, view.viewport3D.camera);
        if (view.tabs_view != null)
        {
            view.tabs_view.selected_tab = 1;
        }
        ui_main_tabs.selected_tab = 0;
    }

    public void MOpt_Play_Start()
    {
        MOpt_Play();
    }

    public void MOpt_Play_Stop()
    {
        Standalone_Stop();
        if (mtab_scene != null)
        {
            for (int i = 0; i < mtab_scene.tab_scenes.children.Count; i++)
            {
                if (mtab_scene.tab_scenes.children[i] is PNL_SceneView ed && ed.game_view != null)
                {
                    ed.game_view.Stop();
                    if (ed.tabs_view != null && ed.tabs_view.selected_tab == 1)
                    {
                        ed.tabs_view.selected_tab = 0;
                    }
                }
            }
        }
        ImpGame.Play_Stop();
    }

    public bool Standalone_IsLive()
    {
        if (_standalone == null)
        {
            return false;
        }
        try
        {
            if (!_standalone.HasExited)
            {
                return true;
            }
            _standalone.Dispose();
        }
        catch
        {
        }
        _standalone = null;
        return false;
    }

    public void Standalone_Stop()
    {
        if (_standalone == null)
        {
            return;
        }
        try
        {
            if (!_standalone.HasExited)
            {
                _standalone.Kill(true);
                _standalone.WaitForExit(2000);
            }
        }
        catch
        {
        }
        try
        {
            _standalone.Dispose();
        }
        catch
        {
        }
        _standalone = null;
    }

    public void Standalone_Play()
    {
        PNL_SceneView view = mtab_scene.ActiveEdScene();
        if (view == null || view.scene == null)
        {
            return;
        }
        ImpScene scene = view.scene;
        if (!scene.File_CanWrite())
        {
            Dialog_Alert.Run("Save the scene before Standalone play.");
            return;
        }
        if (scene.is_dirty)
        {
            scene.File_Write();
            PNL_FileBrowser.Browsers_Notify();
        }

        string exe_name = "Engine";
        if (OperatingSystem.IsWindows())
        {
            exe_name = "Engine.exe";
        }
        string exe = Path.Combine(AppContext.BaseDirectory, exe_name);
        if (!File.Exists(exe))
        {
            Dialog_Alert.Run("Standalone game exe was not found (" + exe_name + ").");
            return;
        }

        Standalone_Stop();

        string scene_arg = File_JSON.Path_Tokenize(scene.filepath);
        string game_arg = "";
        if (A_Game.game != null)
        {
            game_arg = A_Game.game.GetRootDir();
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            UseShellExecute = false,
        };
        if (!string.IsNullOrWhiteSpace(game_arg))
        {
            psi.ArgumentList.Add("--game");
            psi.ArgumentList.Add(game_arg);
        }
        psi.ArgumentList.Add("--scene");
        psi.ArgumentList.Add(scene_arg);

        try
        {
            _standalone = Process.Start(psi);
        }
        catch (Exception e)
        {
            Dialog_Alert.Run("Failed to launch Standalone: " + e.Message);
        }
    }

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        ImpPlayer? _player = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0] : null;
        if (_player == null) return;

        string[] lines =
        {
            "input_hog: " + Label(_player.input_hog),
            "target_focus: " + Label(_player.target_focus),
            "target_cursor: " + Label(_player.target_cursor),
            "target_grabbed: " + Label(_player.target_grabbed),
        };

        Font f = Raylib.GetFontDefault();
        const float fs = 16f;
        const float sp = 1f;
        float line_h = fs + 2f;
        float w = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            float tw = Raylib.MeasureTextEx(f, lines[i], fs, sp).X;
            if (tw > w) w = tw;
        }
        float x = Raylib.GetScreenWidth() - w - 10;
        Raylib.DrawRectangleV(new Vector2(x - 6, 4), new Vector2(w + 12, line_h * lines.Length + 10), new Color(0, 0, 0, 160));
        for (int i = 0; i < lines.Length; i++)
            Raylib.DrawTextEx(f, lines[i], new Vector2(x, 8 + i * line_h), fs, sp, Color.Yellow);

        string Label(ImpComp? c) => c == null ? "null" : c.GetType().Name + (string.IsNullOrEmpty(c.name) ? "" : "  " + c.name);
    }
}