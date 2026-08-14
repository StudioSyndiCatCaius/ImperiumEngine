namespace ImperiumEngine.Assets.UI;

public struct SUI_List
{
    
}

public class UI_List : A_UI
{
    [ImpVar] public UI_Box box_background = null;
    
    
    public static UI_List DEFAULT = new();

    public static bool Draw()
    {
        return true;
    }
}