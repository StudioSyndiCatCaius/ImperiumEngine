

using R3D_cs;

namespace Editor.Panels;

public enum ECameraViewMode
{
    Free,
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right,
}

public enum ECameraRenderMode
{
    Lit,
    Unlit,
    Wireframe,
}

public enum EWorldViewMode
{
    MODE_3D,
    MODE_2D,
    MODE_ALL, //view both 3d and 2d
}



// a panel that displays the 3d world
public class PNL_World : EditorPanel
{
    public Camera view_camera;
    public ECameraViewMode view_mode;
    public ECameraRenderMode render_mode;
    public EWorldViewMode world_view_mode; //mainly used for the level editor, but lets you toggle between 3d and 2d, editing 3d objects or 2d widgets
}