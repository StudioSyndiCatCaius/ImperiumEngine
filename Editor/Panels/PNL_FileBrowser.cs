using Editor.UI;

namespace Editor.Panels;

public class PNL_FileBrowser : EdPanel
{
    public string root_path = "";
    public string filter_string = "";
    
    public UI_Tree tree = new();

    public override void Draw()
    {
        base.Draw();
        
    }
}