using ImperiumCore.Classes;
using ImperiumEngine.Assets;

namespace ImperiumEngine.Objects._2D;

public struct TreeItem
{
    public string label;
    public List<string> column_strings;
    public int flags;
    public List<TreeItem> children;
}

public struct TreeColumn
{
    public string name;
    public int flags;
}

public class O2D_Tree
{
    public TreeItem root;
    public bool enable_DragDrop;
    
    public List<TreeColumn> columns; // always min of 1
    
    public Action<TreeItem> OnItemSelect;
    public Action<TreeItem,bool> OnItemExpand;
}

