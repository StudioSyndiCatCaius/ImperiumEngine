namespace Editor.UI;

public class EUI_SearchBar : EdUi
{
    public string search_text = "";
    
    public Action<string> on_search = null; //Only when string is changed
    public override void OnDraw()
    {
        base.OnDraw();
    }
}