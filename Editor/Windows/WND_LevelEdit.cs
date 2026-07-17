using Editor.Panels;

namespace Editor.Windows;


public class WND_LevelEdit: EditorWindow
{
    public PNL_ComponentTree pnl_level_hierarchy;
    public PNL_Inspector pnl_object_inspector; // inspector for selected entities/components properties
    public PNL_Inspector pnl_level_inspector; // inspector for the level properties
    public PNL_World pnl_world; // the 3d world
}