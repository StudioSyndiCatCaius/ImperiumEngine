using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public class O2D_TabContainer : ImpComponent2D
{
    public O2D_TabBar tabBar = new O2D_TabBar();

    [ImpVar] public bool hiddenTabsDontUpdate = true;

    public int activeTab = 0;

    private readonly List<ImpComponent2D> _pages = new();

    const float TabBarHeight = 36f;

    public O2D_TabContainer()
    {
        tabBar.custom_minimum_size = new Vector2(0, TabBarHeight);
        tabBar.OnSelect = Tab_SetActive;
        Child_Add(tabBar);
    }

    public void Tab_Add(ImpComponent2D page, string label = "")
    {
        int index = _pages.Count;
        if (string.IsNullOrEmpty(label)) label = $"Tab {index + 1}";

        tabBar.Option_Add(label);

        _pages.Add(page);
        Child_Add(page);

        Tabs_Refresh();
    }

    public void Tab_SetActive(int index)
    {
        activeTab = Math.Clamp(index, 0, Math.Max(0, _pages.Count - 1));
        tabBar.activeIndex = activeTab;
        Tabs_Refresh();
    }

    private void Tabs_Refresh()
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            bool active = i == activeTab;
            _pages[i].visible = active;
            if (hiddenTabsDontUpdate) _pages[i].updating = active;
        }
    }

    protected override void Layout_Children(TRect2 rect)
    {
        tabBar.Layout_SetRect(new TRect2(rect.position, new Vector2(rect.size.X, TabBarHeight)));

        var contentRect = new TRect2(
            new Vector2(rect.position.X, rect.position.Y + TabBarHeight),
            new Vector2(rect.size.X, MathF.Max(0f, rect.size.Y - TabBarHeight)));

        foreach (var page in _pages)
            page.Component_Layout(contentRect);
    }
}
