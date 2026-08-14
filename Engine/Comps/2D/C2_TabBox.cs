using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._2D;

public class C2_TabBox : ImpComp2D
{
    public int selected_tab = 0;
    public bool show_tabs = true;
    public float tab_height = 28f;
    public float tab_width = 96f;
    

    public Action<int> on_tab_change;

    public C2_List list_tabs = new()
    {
        alignment = EUIAlignment.Horizontal,
        is_scrollable = false,
        spacing = 0,
    };

    public UiStyle_Box style_background = UiStyle_Box.STYLE_BKG_DARK;
    public UiStyle_Box style_tab_idle = UiStyle_Box.STYLE_TAB_IDLE;
    public UiStyle_Box style_tab_hovered = UiStyle_Box.STYLE_TAB_HOVER;
    public UiStyle_Box style_tab_selected = UiStyle_Box.STYLE_TAB_PRESS;
    public UiStyle_Text text_style = UiStyle_Text.DEFAULT;

    int last_tab = -1;
    int _built_count = -1;

    public C2_TabBox()
    {
        cursor_filter = ECursorFilter.Pass;
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;

        Child_Add(list_tabs);
        list_tabs.on_option_select = OnTabSelect;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        if (!children.Contains(list_tabs))
            Child_Add(list_tabs);

        List<ImpComp2D> pages = new();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] == list_tabs) continue;
            if (children[i] is ImpComp2D p) pages.Add(p);
        }

        if (pages.Count == 0)
        {
            selected_tab = 0;
            list_tabs.is_visible = false;
            if (_built_count != 0)
            {
                list_tabs.Child_RemoveAll();
                _built_count = 0;
            }
            return;
        }

        selected_tab = Math.Clamp(selected_tab, 0, pages.Count - 1);

        if (_built_count != pages.Count)
            RebuildTabs(pages.Count);

        for (int i = 0; i < list_tabs.children.Count; i++)
        {
            if (list_tabs.children[i] is not C2_Button btn) continue;
            string title = TitleOf(i);
            if (btn.text != title) btn.text = title;
            bool sel = i == selected_tab;
            btn.style.style_unhovered = sel ? style_tab_selected : style_tab_idle;
            btn.style.style_hovered = style_tab_hovered;
            btn.style.style_pressed = style_tab_selected;
        }

        TDimensions2 dim = Dimensions_Get();
        float th = show_tabs ? tab_height : 0f;

        list_tabs.is_visible = show_tabs;
        list_tabs.view_alighnment_H = EUIViewportAlignment.Start;
        list_tabs.view_alighnment_V = EUIViewportAlignment.Start;
        list_tabs.size = new Vector2(dim.size.X, th);
        list_tabs.size_min = new Vector2(0, th);
        list_tabs.transform.position = Vector2.Zero;

        float content_h = MathF.Max(0, dim.size.Y - th);
        for (int i = 0; i < pages.Count; i++)
        {
            ImpComp2D page = pages[i];
            page.is_visible = i == selected_tab;
            page.view_alighnment_H = EUIViewportAlignment.Start;
            page.view_alighnment_V = EUIViewportAlignment.Start;
            page.size = new Vector2(dim.size.X, content_h);
            page.size_min = new Vector2(0, 0);
            page.transform.position = new Vector2(0, th);
        }

        if (last_tab != selected_tab)
        {
            last_tab = selected_tab;
            on_tab_change?.Invoke(selected_tab);
        }
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        style_background?.Draw(Dimensions_Get());
    }

    void RebuildTabs(int count)
    {
        list_tabs.Child_RemoveAll();
        for (int i = 0; i < count; i++)
        {
            C2_Button btn = new()
            {
                text = TitleOf(i),
                size = new Vector2(tab_width, tab_height),
                size_min = new Vector2(tab_width, tab_height),
                style = new UiStyle_Button
                {
                    style_unhovered = style_tab_idle,
                    style_hovered = style_tab_hovered,
                    style_pressed = style_tab_selected,
                },
                text_style = text_style,
            };
            list_tabs.Child_Add(btn);
        }
        _built_count = count;
    }

    void OnTabSelect(ImpComp2D c, int i)
    {
        if (i < 0) return;
        selected_tab = i;
    }

    string TitleOf(int i)
    {
        ImpComp? _tab = Child_GetAt(i+1);
        if (_tab != null)
        {
            return _tab.name;
        }
        return "Tab "+i;
    }
}
