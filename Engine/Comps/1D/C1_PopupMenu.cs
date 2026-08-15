using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public struct TPopupMenuOption
{
    public string text;
    public bool is_separator;
    public bool is_disabled;
    public Action on_press;
}

public class C1_PopupMenu : ImpComp
{
    public C2_Box box = new();
    public C2_List list = new();
    public List<TPopupMenuOption> options = new();
    public Action<TPopupMenuOption> on_press;

    public float row_height = 24f;
    public float menu_width = 200f;

    static C2_PopupPanel _open;

    public C1_PopupMenu()
    {
        is_visible = false;
    }

    public static void Open(Vector2 screen_pos, List<TPopupMenuOption> options, Action<TPopupMenuOption> on_press = null)
    {
        C1_PopupMenu menu = new() { options = options ?? new List<TPopupMenuOption>(), on_press = on_press };
        menu.Show(screen_pos);
    }

    public void Show(Vector2 screen_pos)
    {
        CloseOpen();
        ImpComp host = C2_MenuBar.PopupHost() ?? ImpScene.current?.root;
        if (host == null) return;

        C2_PopupPanel panel = new(this)
        {
            style = new UiStyle_Box { tint = new Raylib_cs.Color(36, 36, 36, 255) },
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Start,
                orient_V = EUIViewportAlignment.Start,
            },
        };
        list = new C2_List
        {
            orentation = EUIOrentation.V,
            spacing = 0,
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Fill,
                },
        };
        panel.Child_Add(list);

        float total_h = 4;
        for (int i = 0; i < options.Count; i++)
        {
            TPopupMenuOption opt = options[i];
            if (opt.is_separator)
            {
                Imp2D sep = new()
                {
                    layout = new TLayout2
                    {
                        size = new Vector2(menu_width, 6),
                    },
                    cursor_filter = ECursorFilter.Ignore,
                };
                list.Child_Add(sep);
                total_h += 6;
                continue;
            }

            int captured = i;
            C2_Button btn = new()
            {
                text = opt.text ?? "",
                layout = new TLayout2
                {
                    size = new Vector2(menu_width, row_height),
                    size_min = new Vector2(menu_width, row_height),
                },
                is_disabled = opt.is_disabled,
                content_align_h = EUIPositionAlignment.Start,
                content_pad = 8,
                text_style = UI_Text.LIGHT,
                style = new UI_Button
                {
                    style_unhovered = new UiStyle_Box { tint = new Raylib_cs.Color(36, 36, 36, 255) },
                    style_hovered = new UiStyle_Box { tint = new Raylib_cs.Color(0, 96, 166, 255) },
                    style_pressed = new UiStyle_Box { tint = new Raylib_cs.Color(0, 70, 130, 255) },
                },
            };
            btn.on_click = () => Pick(captured);
            list.Child_Add(btn);
            total_h += row_height;
        }

        panel.layout.size = new Vector2(menu_width, total_h);
        panel.layout.size_min = panel.layout.size;
        panel.transform.position = screen_pos;
        host.Child_Add(panel);
        _open = panel;
        is_visible = true;
        box = panel;
    }

    void Pick(int i)
    {
        if (i < 0 || i >= options.Count) return;
        TPopupMenuOption opt = options[i];
        if (opt.is_separator || opt.is_disabled) return;
        opt.on_press?.Invoke();
        on_press?.Invoke(opt);
        CloseOpen();
        is_visible = false;
    }

    public async Task Run(Action<TPopupMenuOption> _on_press)
    {
        on_press = _on_press;
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        Show(pos);
        while (is_visible) await Task.Yield();
    }

    public static void CloseOpen()
    {
        if (_open == null) return;
        _open.Destroy();
        _open = null;
    }
}

public class C2_PopupPanel : C2_Box
{
    readonly C1_PopupMenu _menu;

    public C2_PopupPanel(C1_PopupMenu menu)
    {
        _menu = menu;
        cursor_filter = ECursorFilter.Pass;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left) && !ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right))
            return;
        if (ImpPlayer.players.Count == 0) return;
        ImpComp target = ImpPlayer.players[0].target_cursor;
        if (C2_MenuBar.IsUnder(this, target)) return;
        _menu.is_visible = false;
        C1_PopupMenu.CloseOpen();
    }
}
