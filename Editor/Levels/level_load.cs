using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Scenes;

//After a project is chosen, this scene is activated while we load the project
public class EditorLevel_Load : ImpLevel
{
    public A_Texture2D logo;
    public O2D_Image ui_logo;
}