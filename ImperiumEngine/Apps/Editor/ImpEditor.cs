using ImperiumEngine.Source.Objects._2D;

namespace ImperiumEngine.Apps.Editor;

public class ImpEditor
{
    public O2D_FileExplorer o_FileExplorer = new O2D_FileExplorer();
    public O2D_SceneOutliner o_Outliner = new O2D_SceneOutliner();
    public List<O2D_SceneViewer> o_OpenScenes = new List<O2D_SceneViewer>();
}