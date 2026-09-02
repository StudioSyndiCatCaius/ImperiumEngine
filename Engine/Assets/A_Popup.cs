using Engine.Core;

namespace Engine.Assets;

public struct TPopupOption
{
    public string text = "";
    public string shortcut = "";
    public Action on_select = null;
    public List<TPopupOption> suboptions = new();

    public TPopupOption()
    {
    }
}

//config for a popup menu
public class A_Popup : ImpAsset
{
    public List<TPopupOption> options = new();
    public Action<TPopupOption> on_select;
}
