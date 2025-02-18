namespace ImperiumEngine.Source.Objects._2D;

public class O2D_FileExplorer: ImpComponent2D
{
    private int    _DisplayStyle = 0;
    private Int32  _DisplayScale = 10;
    private bool   bTreeOpen     = true;
    private string[] pathsRoot;
    private string[] pathsHidden;

    private Action<string> OnFileClickL;
    private Action<string> OnFileClickR;
}