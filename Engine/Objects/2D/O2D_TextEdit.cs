using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;
using Silk.NET.Input;

namespace ImperiumEngine.Objects._2D;

public class O2D_TextEdit : ImpComponent2D
{
    public string text = "";
    public Action<string>? OnTextChanged;
    
    [ImpVar][Exposed] public bool wrap=true;
    [ImpVar][Exposed] public bool allowSelect=true;
    [ImpVar][Exposed] public bool allowCopy=true;

    private int    _cursor;
    private double _blinkTimer;
    private bool   _cursorVisible = true;

    private static readonly Color C_Bg       = Color.FromArgb(28, 28, 34);
    private static readonly Color C_BgHover  = Color.FromArgb(38, 38, 46);
    private static readonly Color C_BgFocus  = Color.FromArgb(22, 22, 30);
    private static readonly Color C_Text     = Color.FromArgb(215, 215, 225);
    private static readonly Color C_Placeholder = Color.FromArgb(70, 70, 82);
    private static readonly Color C_Border   = Color.FromArgb(80, 140, 255);
    private static readonly Color C_Cursor   = Color.FromArgb(180, 210, 255);

    public O2D_TextEdit()
    {
        mouse_filter        = EMouseFilter.Stop;
        custom_minimum_size = new Vector2(0, 24);
    }

    // -----------------------------------------------------------------------------------------

    public override void OnFocus()
    {
        _cursor      = text.Length;
        _cursorVisible = true;
        _blinkTimer  = 0;
    }

    public override void OnUnfocus()
    {
        _cursorVisible = false;
    }

    public override void OnKeyChar(char c)
    {
        if (char.IsControl(c)) return;
        text = text[.._cursor] + c + text[_cursor..];
        _cursor++;
        Notify();
        Cursor_ResetBlink();
    }

    public override void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Backspace:
                if (_cursor > 0) { text = text[..(_cursor - 1)] + text[_cursor..]; _cursor--; Notify(); }
                break;
            case Key.Delete:
                if (_cursor < text.Length) { text = text[.._cursor] + text[(_cursor + 1)..]; Notify(); }
                break;
            case Key.Left:
                if (_cursor > 0) _cursor--;
                break;
            case Key.Right:
                if (_cursor < text.Length) _cursor++;
                break;
            case Key.Home:
                _cursor = 0;
                break;
            case Key.End:
                _cursor = text.Length;
                break;
            case Key.Escape:
                ImpApp.Active?.players[0].Focus_Clear();
                return;
        }
        Cursor_ResetBlink();
    }

    public override void OnUpdate(double dt)
    {
        if (!focused) return;
        _blinkTimer += dt;
        if (_blinkTimer >= 0.53)
        {
            _blinkTimer -= 0.53;
            _cursorVisible = !_cursorVisible;
        }
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        var bg = focused ? C_BgFocus : hovered ? C_BgHover : C_Bg;
        rhi.Draw2D_Rect(
            TVector2i.From(_rect.position + new Vector2(1, 1)),
            TVector2i.From(_rect.size    - new Vector2(2, 2)), bg);

        if (focused)
            rhi.Draw2D_Rect(
                new TVector2i((int)(_rect.position.X + 1), (int)(_rect.position.Y + _rect.size.Y - 2)),
                new TVector2i((int)(_rect.size.X - 2), 2), C_Border);

        float tx = _rect.position.X + 6;
        float ty = _rect.position.Y + (_rect.size.Y - 14) * 0.5f;

        if (!string.IsNullOrEmpty(text))
            rhi.Draw2D_Text(new TVector2i((int)tx, (int)ty), text, 13, C_Text);
        else if (!focused)
            rhi.Draw2D_Text(new TVector2i((int)tx, (int)ty), "…", 13, C_Placeholder);

        if (focused && _cursorVisible)
        {
            var measured = rhi.Text_Measure(_cursor > 0 ? text[.._cursor] : "", 13);
            rhi.Draw2D_Rect(
                new TVector2i((int)(tx + measured.x), (int)ty),
                new TVector2i(1, 14), C_Cursor);
        }
    }

    // -----------------------------------------------------------------------------------------

    private void Notify() => OnTextChanged?.Invoke(text);

    private void Cursor_ResetBlink() { _blinkTimer = 0; _cursorVisible = true; }
}
