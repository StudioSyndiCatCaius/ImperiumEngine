using Editor.UI;
using Engine.Assets;
using Engine.Core;

namespace Editor.Panels;

public class PNL_SceneTree : EdPanel
{
    public EUI_SearchBar comp_search = new();
    
    A_Scene? scene;
    ImpComp? root_comp;
    
    public override void OnDraw()
    {
        base.OnDraw();
        
    }
}