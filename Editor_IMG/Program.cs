using Editor;
using Editor.Scenes;
using Engine;
using Engine.Assets;
using Engine.Core;
using ImGuiNET;


App app = new();

App.scene_current = new SCN_Editor();

app.Run(new()
{
    on_post_init = () =>
    {
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    },
});

