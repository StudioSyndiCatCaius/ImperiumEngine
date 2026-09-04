using Editor;
using Editor.Scenes;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;


namespace Editor
{
    static class Editor
    {
        public static THistory history = new();
        
        static void Main(string[] args)
        {
            App app = new();

            App.scene_current = new SCN_Editor();

            app.Run(new()
            {
                on_post_init = () =>
                {
                    ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
                    Raylib.MaximizeWindow();
                },
            });

        }
    }
}


