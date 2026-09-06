using Editor;
using Editor.Scenes;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;


namespace Editor
{
    static class Editor
    {
        public static THistory history = new();
        
        static void Main(string[] args)
        {
            App app = new();

            App.scene_current = new SCN_Editor();
            App.name = "Imperium Editor";

            app.Run(new()
            {
                on_pre_init = () =>
                {
                    string game = App.getArg_String("game");
                    if (string.IsNullOrEmpty(game))
                        game = Path.Combine(GFile.GetDir_Root(EContentDir.Engine), "Templates", "Test", "Test.ImpGame");
                    if (File.Exists(game))
                    {
                        App.game_file = game;
                        App.game_data = TTable.FromTOML(GFile.LoadAs_String(game));
                    }

                    rlImGui.SetupUserFonts = io =>
                    {
                        string font = GFile.Make_Path_Absolute("{engine}/Fonts/Arial/Arial.ttf");
                        if (File.Exists(font))
                            io.Fonts.AddFontFromFileTTF(font, 16f);
                        else
                            io.Fonts.AddFontDefault();
                    };
                },
                on_post_init = () =>
                {
                    ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
                    EdTheme.Apply("{engine}/Themes/default.toml");
                    EdIcons.Load();
                    if (App.scene_current?.root is SNC_Editor_Root root)
                        EdConfig.Load(root);
                    Raylib.MaximizeWindow();
                },
                on_shutdown = () =>
                {
                    SNC_Editor_Root.Play_Stop();
                    if (App.scene_current?.root is SNC_Editor_Root root)
                        EdConfig.Save(root);
                },
            });

        }
    }

    //makes this property as being written to the projects Config/Editor.Toml file 
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class EdConfigAttribute : Attribute
    {
        
    }
}


