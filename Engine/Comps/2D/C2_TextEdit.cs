using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_TextEdit : Imp2D
{
    [ImpVar] public string text = "";
    [ImpVar] public string text_placeholder = "";
    [ImpVar] public bool is_focused;
    [ImpVar] public bool is_password;
    [ImpVar] public bool hog_input = true;
    [ImpVar] public UI_TextEdit style = new();

    public Action<string> on_text_changed;
    public Action<string> on_text_cleared;
    public Action<string> on_submit;

    public int cursor;
    double _blink;

    public C2_TextEdit()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        _blink += dt;

        if (is_focused && ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (ImpPlayer.popup_menu_open && !ImpPlayer.Popup_Contains(this))
            {
                is_focused = false;
                Hog_Release(p);
            }
            else if (ImpDialog.IsOpen && !ImpDialog.Contains(this))
            {
                is_focused = false;
                Hog_Release(p);
            }
            else if (ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left) && !ClickIsOurs(p.target_cursor))
            {
                is_focused = false;
                Hog_Release(p);
            }
        }

        if (!is_focused) return;
        if (text == null) text = "";
        cursor = Math.Clamp(cursor, 0, text.Length);

        int ch = Raylib.GetCharPressed();
        while (ch > 0)
        {
            if (ch >= 32 && ch != 127)
            {
                text = text.Insert(cursor, char.ConvertFromUtf32(ch));
                cursor++;
                on_text_changed?.Invoke(text);
            }
            ch = Raylib.GetCharPressed();
        }

        bool ctrl = ImpPlayer.Key_IsDown(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsDown(EInputKey.Key_RightControl);

        if (PressedOrRepeat(EInputKey.Key_Backspace) && cursor > 0)
        {
            text = text.Remove(cursor - 1, 1);
            cursor--;
            on_text_changed?.Invoke(text);
            if (text.Length == 0) on_text_cleared?.Invoke(text);
        }
        if (PressedOrRepeat(EInputKey.Key_Delete) && cursor < text.Length)
        {
            text = text.Remove(cursor, 1);
            on_text_changed?.Invoke(text);
            if (text.Length == 0) on_text_cleared?.Invoke(text);
        }
        if (PressedOrRepeat(EInputKey.Key_Left) && cursor > 0) cursor--;
        if (PressedOrRepeat(EInputKey.Key_Right) && cursor < text.Length) cursor++;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Home)) cursor = 0;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_End)) cursor = text.Length;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Enter) || ImpPlayer.Key_IsPressed(EInputKey.Key_KpEnter))
            on_submit?.Invoke(text ?? "");

        if (ctrl && ImpPlayer.Key_IsPressed(EInputKey.Key_A))
            cursor = text.Length;
    }

    static bool PressedOrRepeat(EInputKey key)
    {
        if (ImpPlayer.Key_IsPressed(key)) return true;
        return Raylib.IsKeyPressedRepeat((KeyboardKey)(int)key);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        style ??= new UI_TextEdit();
        style.background?.Draw(dim);

        string shown = text ?? "";
        if (is_password && shown.Length > 0) shown = new string('•', shown.Length);

        bool empty = string.IsNullOrEmpty(shown);
        UI_Text ts = empty ? style.placeholder_style : style.text_style;
        string draw = empty ? (text_placeholder ?? "") : shown;
        Vector2 pad = new(6, 0);
        Vector2 pos = dim.position + pad;
        Vector2 sz = dim.size - new Vector2(pad.X * 2, 0);
        ts?.Draw(draw, pos, sz, 0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        if (is_focused && (_blink % 1.0) < 0.55 && ts != null)
        {
            Font font = ts.font != null ? ts.font.font : Raylib.GetFontDefault();
            float fs = ts.size > 0 ? ts.size : 16;
            string prefix = empty ? "" : (cursor <= shown.Length ? shown[..cursor] : shown);
            float cx = pos.X + Raylib.MeasureTextEx(font, prefix, fs, 1f).X;
            float ch = Raylib.MeasureTextEx(font, "Ay", fs, 1f).Y;
            float cy = pos.Y + (sz.Y - ch) * 0.5f;
            Raylib.DrawRectangleV(new Vector2(cx, cy), new Vector2(1, ch), ts.color);
        }
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A) return;
        is_focused = true;
        _blink = 0;
        cursor = (text ?? "").Length;
        if (hog_input)
        {
            player.input_hog = this;
        }
    }

    void Hog_Release(ImpPlayer player)
    {
        if (player != null && player.input_hog == this)
        {
            player.input_hog = null;
        }
    }

    bool ClickIsOurs(ImpComp target)
    {
        for (ImpComp n = target; n != null; n = n.parent)
        {
            if (n == this)
            {
                return true;
            }
        }
        return false;
    }
}

public class UI_TextEdit : ImpAsset
{
    [ImpVar] public UI_Box background = UI_Box.BkgMid;
    [ImpVar] public UI_Text text_style = UI_Text.LIGHT;
    [ImpVar] public UI_Text placeholder_style = UI_Text.MUTED;
}
