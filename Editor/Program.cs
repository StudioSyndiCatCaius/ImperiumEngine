using Editor.Scenes;
using Engine;
using Engine.Core;
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
        
        [STAThread]
        static void Main(string[] args)
        {
            App app = new();

            App.scene_current = new SCN_Projects();
            App.name = "Imperium Editor";

            app.Run(new()
            {
                on_pre_init = () =>
                {
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
                    Raylib.MaximizeWindow();

                    if (App.scene_current is SCN_Projects proj)
                        proj.LoadGlobal();

                    string game = App.getArg_String("game");
                    if (!string.IsNullOrEmpty(game) && File.Exists(game))
                        SCN_Projects.OpenProject(game);
                    else if (App.scene_current is SCN_Projects p
                             && p.autoload_last_project
                             && !string.IsNullOrEmpty(p.last_project_path)
                             && File.Exists(p.last_project_path))
                        SCN_Projects.OpenProject(p.last_project_path);
                },
                on_shutdown = () =>
                {
                    SNC_Editor_Root.Play_Stop();
                    if (App.scene_current?.root is SNC_Editor_Root root)
                        EdConfig.Save(root);
                    if (App.scene_current is SCN_Projects proj)
                        proj.SaveGlobal();
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


