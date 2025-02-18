namespace ImperiumEngine.Source.Objects._2D;

public class O2D_Tree: ImpComponent2D
{
    
}

public class O2D_FileExplorer : O2D_Tree
{
    public string root_path ="";
    public List<string> filter_extentsions =new();
}

public class O2D_SceneOutliner : O2D_Tree
{
    public ImpObject root_object = new();
}