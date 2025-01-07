using ImGuiNET;
using ImperiumEngine.Source.Nodes._2D;

namespace ImperiumEngine;

public class Imperium_Editor : ImpApp
{
    public override void Init(ImpObject root)
    {
        N2D_FileExplorer window_explorer = Object_Create<N2D_FileExplorer>("Content Explorer",root);
        N2D_Panel panelOutliner = Object_Create<N2D_Panel>("Scene Outliner",root);
        N2D_Panel panelView = Object_Create<N2D_Panel>("Scene View",root);
        N2D_Properties panel_properties = Object_Create<N2D_Properties>("Property Editor",root);

        N2D_MenuBar menu_main = Object_Create<N2D_MenuBar>("Menu", root);
        
        base.Init(root);
    }

    public override void Draw(double delta)
    {
        
        base.Draw(delta);
        
    }
}