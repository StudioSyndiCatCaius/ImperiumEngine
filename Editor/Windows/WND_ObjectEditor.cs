using ImperiumCore.Classes;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Windows;

//Pulse Editor Window. This is the editor for any pbject using the Pulse Visual Scriting. Pulse is VERY similar frontend to UE's blueprints
public class WND_ObjectEditor : EditorWindow
{
    public ImpObject Object;
    
    public O2D_GraphEdit ui_graph;
    public O2D_Tree ui_tree_PulseFunctions; // Cascading tree of categories functions. tree is category & subcategories until function at the bottom
    public O2D_Tree ui_tree_PulseVariables; // Cascading tree of categories variables. tree is category & subcategories until function at the bottom
    
    public O2D_Inspector ui_inspector;
}