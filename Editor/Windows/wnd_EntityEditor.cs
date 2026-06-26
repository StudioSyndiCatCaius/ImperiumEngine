using ImperiumEngine.Objects._2D;
using ImperiumEngine.Objects._2D.Trees;

namespace ImperiumEditor.Windows;


public enum EEntityViewMode
{
    View_3D,
    View_2D,
    View_ALL,
}


public class wnd_EntityEditor : wnd_PulseEditor
{
    public O2D_Tree_Outliner ui_outliner; // tree outline for all components used by the entity
    public O2D_PropertyEdit ui_inspector; // proprty inspector for this entity
    
    public EEntityViewMode view_mode;
}