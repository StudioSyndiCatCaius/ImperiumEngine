using ImperiumCore.Classes;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Windows;

//Pulse Editor Window. This is the editor for any pbject using the Pulse Visual Scriting. Pulse is VERY VERY similar frontend to UE's blueprints
public class wnd_PulseEditor : EditorWindow
{
    public ImpObject Object;
    
    public O2D_GraphEdit ui_graph;
    public O2D_Tree ui_tree_PulseFunctions;
    public O2D_Tree ui_tree_PulseVariables;
    
    public O2D_PropertyEdit ui_inspector;
}