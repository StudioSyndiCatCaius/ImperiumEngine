using ImGuiNET;
using ImperiumEngine.Source.Nodes._2D;

namespace ImperiumEngine;

public class Imperium_Editor : ImpApp
{
    public override void Init(ImpObject root)
    {
        N2D_FileExplorer window_explorer = Object_Create<N2D_FileExplorer>("Content Explorer",root);
        N2D_Window window_outliner = Object_Create<N2D_Window>("Scene Outliner",root);
        N2D_Window window_view = Object_Create<N2D_Window>("Scene View",root);
        N2D_Window window_node = Object_Create<N2D_Window>("Node Editor",root);

        N2D_MenuBar menu_main = Object_Create<N2D_MenuBar>("Menu", root);
        
        base.Init(root);
    }

    public override void Draw(double delta)
    {
        
        base.Draw(delta);
        
    }
}