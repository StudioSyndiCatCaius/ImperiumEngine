using ImperiumCore.Assets;

namespace ImperiumEngine.Objects._2D.Trees;

public struct T_FileTree_ItemConfig
{
    public A_Texture icon;
    public bool is_folder;
    public List<string> extensions;
}

public class O2D_Tree_FileExplorer : O2D_Tree
{
    public string root_path;
    
    //when updated, changes the displayed list of files and folders to only those that have this string in their name
    public string name_filter { get; set;}

    public List<T_FileTree_ItemConfig> item_configs;
}