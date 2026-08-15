using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_ColorPicker : Imp2D
{
    const float PanelW = 224f;
    const float SvH = 128f;
    const float BarH = 14f;
    const float Pad = 6f;

    [ImpVar] public Color color = Color.White;
    [ImpVar] public bool show_alpha = true;
    public Action<C2_ColorPicker> on_color_changed;

    bool _open;
    bool _hover;
    C2_ColorPickerPanel _panel;
    float _hue, _sat = 1, _val = 1, _alpha = 1;

    public bool IsOpen => _open;

    public C2_ColorPicker()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
        HSV_Sync();
    }

    public void Color_Set(Color value, bool notify = true)
    {
        color = value;
        HSV_Sync();
        if (notify) on_color_changed?.Invoke(this);
    }

    public void Color_SetQuiet(Color value)
    {
        if (value.R == color.R && value.G == color.G && value.B == color.B && value.A == color.A) return;
        color = value;
        HSV_Sync();
    }

    void HSV_Sync()
    {
        Vector3 hsv = Raylib.ColorToHSV(color);
        if (hsv.Y > 0f && hsv.Z > 0f) _hue = hsv.X;
        _sat = hsv.Y;
        _val = hsv.Z;
        _alpha = color.A / 255f;
    }

    internal void HSV_Apply(float hue, float sat, float val, float alpha)
    {
        _hue = hue; _sat = sat; _val = val; _alpha = alpha;
        Color rgb = Raylib.ColorFromHSV(_hue, _sat, _val);
        color = new Color(rgb.R, rgb.G, rgb.B, (byte)Math.Clamp(_alpha * 255f, 0, 255));
        on_color_changed?.Invoke(this);
    }

    internal void GetHSV(out float hue, out float sat, out float val, out float alpha)
    {
        hue = _hue; sat = _sat; val = _val; alpha = _alpha;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;
        UiStyle_Box bg = _open ? UiStyle_Box.STYLE_BTN_PRESS : _hover ? UiStyle_Box.STYLE_BTN_HOVER : UiStyle_Box.STYLE_BKG_MID;
        bg.Draw(dim);
        var sw = new Rectangle(dim.position.X + 2, dim.position.Y + 2, MathF.Max(0, dim.size.X - 4), MathF.Max(0, dim.size.Y - 4));
        if (color.A < 255)
        {
            for (int y = 0; y < sw.Height; y += 5)
            for (int x = 0; x < sw.Width; x += 5)
                Raylib.DrawRectangle((int)(sw.X + x), (int)(sw.Y + y), 5, 5,
                    ((x / 5 + y / 5) & 1) == 0 ? new Color(70, 70, 70, 255) : new Color(50, 50, 50, 255));
        }
        Raylib.DrawRectangleRec(sw, color);
        Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1,
            _open ? new Color(0, 120, 215, 255) : new Color(80, 80, 80, 255));
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin) _hover = true;
        else if (notify == ENotifyGeneric.End) _hover = false;
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
            _panel?.Destroy();
            _panel = null;
            return;
        }
        HSV_Sync();
        ImpComp host = C2_MenuBar.PopupHost();
        if (host == null) { _open = false; return; }
        TDimensions2 dim = Dimensions_Get();
        _panel = new C2_ColorPickerPanel(this)
        {
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Start,
                    orient_V = EUIViewportAlignment.Start,
                },
        };
        float h = Pad + SvH + Pad + BarH + Pad + (show_alpha ? BarH + Pad : 0) + 22 + Pad;
        _panel.layout.size = new Vector2(MathF.Max(PanelW, dim.size.X), h);
        _panel.layout.size_min = _panel.layout.size;
        _panel.transform.position = new Vector2(dim.position.X, dim.position.Y + dim.size.Y);
        host.Child_Add(_panel);
    }

    internal void Close() => Open_Set(false);
}

class C2_ColorPickerPanel : C2_Box
{
    readonly C2_ColorPicker _owner;
    int _area = -1;

    public C2_ColorPickerPanel(C2_ColorPicker owner)
    {
        _owner = owner;
        cursor_filter = ECursorFilter.Hit;
        style = new UiStyle_Box { texture = null, tint = new Color(40, 40, 42, 255) };
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer p = ImpPlayer.players[0];
        TDimensions2 dim = Dimensions_Get();
        Layout(dim, out Rectangle sv, out Rectangle hue, out Rectangle alpha, out _);

        if (ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left))
        {
            Vector2 m = p.cursor.position;
            if (Raylib.CheckCollisionPointRec(m, sv)) _area = 0;
            else if (Raylib.CheckCollisionPointRec(m, hue)) _area = 1;
            else if (_owner.show_alpha && Raylib.CheckCollisionPointRec(m, alpha)) _area = 2;
            else if (!p.Cursor_IsInDimensions(dim) && p.target_cursor != _owner)
            {
                _owner.Close();
                return;
            }
        }
        if (!ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left)) _area = -1;
        if (_area < 0) return;

        Vector2 pos = p.cursor.position;
        _owner.GetHSV(out float h, out float s, out float v, out float a);
        if (_area == 0)
        {
            s = Frac(pos.X, sv.X, sv.Width);
            v = 1f - Frac(pos.Y, sv.Y, sv.Height);
        }
        else if (_area == 1) h = Frac(pos.X, hue.X, hue.Width) * 360f;
        else a = Frac(pos.X, alpha.X, alpha.Width);
        _owner.HSV_Apply(h, s, v, a);
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        Layout(dim, out Rectangle sv, out Rectangle hue, out Rectangle alpha, out Rectangle preview);
        Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1, new Color(80, 80, 80, 255));

        _owner.GetHSV(out float huev, out float sat, out float val, out float alp);
        Color pure = Raylib.ColorFromHSV(huev, 1, 1);
        Raylib.DrawRectangleGradientH((int)sv.X, (int)sv.Y, (int)sv.Width, (int)sv.Height, Color.White, pure);
        Raylib.DrawRectangleGradientV((int)sv.X, (int)sv.Y, (int)sv.Width, (int)sv.Height, new Color(0, 0, 0, 0), Color.Black);
        Raylib.DrawRectangleLinesEx(sv, 1, new Color(80, 80, 80, 255));
        Cursor(new Vector2(sv.X + sat * sv.Width, sv.Y + (1 - val) * sv.Height));

        const int steps = 6;
        float w = hue.Width / steps;
        for (int i = 0; i < steps; i++)
        {
            Color a = Raylib.ColorFromHSV(i * 60f, 1, 1);
            Color b = Raylib.ColorFromHSV((i + 1) * 60f, 1, 1);
            Raylib.DrawRectangleGradientH((int)(hue.X + i * w), (int)hue.Y, (int)MathF.Ceiling(w), (int)hue.Height, a, b);
        }
        Marker(hue, huev / 360f);

        if (_owner.show_alpha)
        {
            Color op = new Color(_owner.color.R, _owner.color.G, _owner.color.B, (byte)255);
            Raylib.DrawRectangleGradientH((int)alpha.X, (int)alpha.Y, (int)alpha.Width, (int)alpha.Height,
                new Color(op.R, op.G, op.B, (byte)0), op);
            Marker(alpha, alp);
        }

        Raylib.DrawRectangleRec(preview, _owner.color);
        Raylib.DrawRectangleLinesEx(preview, 1, new Color(80, 80, 80, 255));
        UI_Text.LIGHT.Draw($"#{_owner.color.R:X2}{_owner.color.G:X2}{_owner.color.B:X2}{_owner.color.A:X2}",
            new Vector2(preview.X + preview.Height * 1.6f + 6, preview.Y),
            new Vector2(MathF.Max(0, preview.Width - preview.Height * 1.6f - 8), preview.Height),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
    }

    void Layout(TDimensions2 dim, out Rectangle sv, out Rectangle hue, out Rectangle alpha, out Rectangle preview)
    {
        float x = dim.position.X + 6;
        float y = dim.position.Y + 6;
        float inner = dim.size.X - 12;
        sv = new Rectangle(x, y, inner, 128);
        y += 134;
        hue = new Rectangle(x, y, inner, 14);
        y += 20;
        if (_owner.show_alpha) { alpha = new Rectangle(x, y, inner, 14); y += 20; }
        else alpha = default;
        preview = new Rectangle(x, y, inner, 22);
    }

    static float Frac(float v, float start, float length) =>
        length <= 0 ? 0 : Math.Clamp((v - start) / length, 0, 1);

    static void Cursor(Vector2 at)
    {
        var box = new Rectangle(at.X - 4.5f, at.Y - 4.5f, 9, 9);
        Raylib.DrawRectangleLinesEx(box, 2, Color.Black);
        Raylib.DrawRectangleLinesEx(new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), 1, Color.White);
    }

    static void Marker(Rectangle bar, float t)
    {
        float x = bar.X + Math.Clamp(t, 0, 1) * bar.Width;
        var box = new Rectangle(x - 2, bar.Y - 2, 4, bar.Height + 4);
        Raylib.DrawRectangleLinesEx(box, 2, Color.Black);
        Raylib.DrawRectangleLinesEx(new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), 1, Color.White);
    }
}
