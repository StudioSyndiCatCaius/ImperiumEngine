using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public struct TDropdownOption
{
    public string name;
    public TDropdownOption(string name) { this.name = name; }
}

public class C2_Dropdown : Imp2D
{
    [ImpVar] public int current_option = -1;
    [ImpVar] public List<TDropdownOption> options = new();
    public string placeholder_text = "";

    public UiStyle_Box style_idle = UiStyle_Box.STYLE_BKG_MID;
    public UiStyle_Box style_hover = UiStyle_Box.STYLE_BTN_HOVER;
    public UiStyle_Box style_open = UiStyle_Box.STYLE_BTN_PRESS;
    public UI_Text Text = UI_Text.LIGHT;

    public Action<C2_Dropdown> on_dropdown_open;
    public Action<C2_Dropdown> on_dropdown_close;
    public Action<C2_Dropdown, TDropdownOption, int> on_dropdown_change;

    bool _open;
    bool _hover;

    public bool IsOpen => _open;

    public C2_Dropdown()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    public void Option_SetQuiet(int index) => current_option = index;

    public void Options_Set(IEnumerable<string> names)
    {
        options.Clear();
        if (names == null) return;
        foreach (string n in names) options.Add(new TDropdownOption(n));
    }

    string Label()
    {
        return current_option >= 0 && current_option < options.Count
            ? options[current_option].name
            : placeholder_text;
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        UiStyle_Box bg = _open ? style_open : (_hover ? style_hover : style_idle);
        bg?.Draw(dim);

        float pad = 6;
        Text?.Draw(Label(), dim.position + new Vector2(pad, 0),
            new Vector2(MathF.Max(0, dim.size.X - pad * 3 - 8), dim.size.Y),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        float cx = dim.position.X + dim.size.X - 10;
        float cy = dim.position.Y + dim.size.Y * 0.5f;
        Color ac = Text?.color ?? Color.White;
        Raylib.DrawTriangle(
            new Vector2(cx - 4, cy - 2),
            new Vector2(cx + 4, cy - 2),
            new Vector2(cx, cy + 3), ac);
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin) _hover = true;
        else if (notify == ENotifyGeneric.End) _hover = false;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_open && !ImpPlayer.popup_menu_open)
        {
            _open = false;
            if (on_dropdown_close != null)
            {
                on_dropdown_close(this);
            }
        }
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A) return;
        Open_Set(!_open);
    }

    void Open_Set(bool open)
    {
        if (open == _open) return;
        _open = open;
        if (!open)
        {
            if (ImpPlayer.popup_menu_open && ImpPlayer.popup_menu_target == this)
            {
                ImpPlayer.Popup_Close();
            }
            on_dropdown_close?.Invoke(this);
            return;
        }

        on_dropdown_open?.Invoke(this);
        List<TPopupMenuOption> opts = new();
        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            opts.Add(new TPopupMenuOption
            {
                text = options[i].name ?? "",
                on_press = () =>
                {
                    current_option = idx;
                    _open = false;
                    on_dropdown_change?.Invoke(this, options[idx], idx);
                    on_dropdown_close?.Invoke(this);
                },
            });
        }
        TDimensions2 dim = Dimensions_Get();
        ImpPlayer.Popup_Run(this, new A_PopupConfig { options = opts }, null,
            new Vector2(dim.position.X, dim.position.Y + dim.size.Y));
    }
}
