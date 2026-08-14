using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// A component for creating separators in the UI that can be dragged to resize UI elements.
public class C2_Seperator : ImpComp2D
{
    [ImpVar] public EUIAlignment alignment = EUIAlignment.Horizontal;
    [ImpVar] public float thickness = 10f;
    [ImpVar] public bool is_draggable = true;
    public UiStyle_Seperator style = UiStyle_Seperator.DEFAULT;

    bool _hover;
    bool _drag;
    Vector2 _drag_origin;
    ImpComp2D? _prev;
    ImpComp2D? _next;
    float _prev_main;
    float _next_main;

    public C2_Seperator()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
        size = new Vector2(8, 8);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        cursor_filter = ECursorFilter.Hit;

        if (parent is C2_List list)
            alignment = list.alignment;

        bool horizontal = alignment == EUIAlignment.Horizontal;
        float thick = thickness > 0 ? thickness : 8f;
        if (horizontal)
        {
            size.X = thick;
            view_alighnment_H = EUIViewportAlignment.Start;
            view_alighnment_V = EUIViewportAlignment.Fill;
        }
        else
        {
            size.Y = thick;
            view_alighnment_H = EUIViewportAlignment.Fill;
            view_alighnment_V = EUIViewportAlignment.Start;
        }

        if (!is_draggable || ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];
        bool held = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool targeted = player.cursor_target == this;

        if (!_drag && targeted && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left)
            && (player.input_hog == null || player.input_hog == this))
        {
            Neighbors(out _prev, out _next);
            if (_prev != null && _next != null)
            {
                _drag = true;
                _drag_origin = player.cursor.position;
                _prev_main = MainOf(_prev, horizontal);
                _next_main = MainOf(_next, horizontal);
                player.input_hog = this;
                Cursor_Set(horizontal);
            }
        }

        if (_drag && player.input_hog == this)
        {
            float delta = horizontal
                ? player.cursor.position.X - _drag_origin.X
                : player.cursor.position.Y - _drag_origin.Y;
            ApplySplit(horizontal, delta);
            if (!held)
            {
                _drag = false;
                if (player.input_hog == this) player.input_hog = null;
                if (!_hover) Cursor_Clear();
            }
        }
        else if (_drag && player.input_hog != this)
        {
            _drag = false;
            if (!_hover) Cursor_Clear();
        }
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        style ??= UiStyle_Seperator.DEFAULT;
        Color tint = _drag ? style.tint_pressed : _hover ? style.tint_hovered : style.tint;
        bool horizontal = alignment == EUIAlignment.Horizontal;

        if (style.image != null && style.image.texture.Id != 0)
        {
            Texture2D tex = style.image.texture;
            Rectangle src = new(0, 0, tex.Width, tex.Height);
            if (horizontal)
            {
                Raylib.DrawTexturePro(tex, src,
                    new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y),
                    Vector2.Zero, 0f, tint);
            }
            else
            {
                Vector2 center = dim.position + dim.size * 0.5f;
                Raylib.DrawTexturePro(tex, src,
                    new Rectangle(center.X, center.Y, dim.size.Y, dim.size.X),
                    new Vector2(dim.size.Y * 0.5f, dim.size.X * 0.5f),
                    90f, tint);
            }
        }
        else
            Raylib.DrawRectangleV(dim.position, dim.size, tint);
    }

    public override void Cursor_OnEnter(ImpPlayer player)
    {
        base.Cursor_OnEnter(player);
        _hover = true;
        if (is_draggable) Cursor_Set(alignment == EUIAlignment.Horizontal);
    }

    public override void Cursor_OnExit(ImpPlayer player)
    {
        base.Cursor_OnExit(player);
        if (!_drag)
        {
            _hover = false;
            Cursor_Clear();
        }
    }

    void Neighbors(out ImpComp2D? prev, out ImpComp2D? next)
    {
        prev = null;
        next = null;
        if (parent == null) return;
        List<ImpComp> kids = parent.children;
        int self = kids.IndexOf(this);
        if (self < 0) return;
        for (int i = self - 1; i >= 0; i--)
        {
            if (kids[i] is ImpComp2D d && d.is_visible && d is not C2_Seperator)
            {
                prev = d;
                break;
            }
        }
        for (int i = self + 1; i < kids.Count; i++)
        {
            if (kids[i] is ImpComp2D d && d.is_visible && d is not C2_Seperator)
            {
                next = d;
                break;
            }
        }
    }

    void ApplySplit(bool horizontal, float delta)
    {
        if (_prev == null || _next == null) return;

        float min_p = MinOf(_prev, horizontal);
        float min_n = MinOf(_next, horizontal);
        float max_p = MaxOf(_prev, horizontal);
        float max_n = MaxOf(_next, horizontal);
        float total = _prev_main + _next_main;
        float lo = min_p;
        float hi = total - min_n;
        if (max_p > 0) hi = MathF.Min(hi, max_p);
        if (max_n > 0) lo = MathF.Max(lo, total - max_n);
        if (hi < lo) hi = lo;
        float new_p = Math.Clamp(_prev_main + delta, lo, hi);
        float new_n = total - new_p;

        bool prev_fill = IsFill(_prev, horizontal);
        bool next_fill = IsFill(_next, horizontal);
        SetMain(_prev, horizontal, new_p);
        SetMain(_next, horizontal, new_n);
        if (prev_fill) _prev.stretch_ratio = MathF.Max(0.001f, new_p);
        if (next_fill) _next.stretch_ratio = MathF.Max(0.001f, new_n);
    }

    static bool IsFill(ImpComp2D c, bool horizontal) =>
        horizontal
            ? c.view_alighnment_H == EUIViewportAlignment.Fill
            : c.view_alighnment_V == EUIViewportAlignment.Fill;

    static float MainOf(ImpComp2D c, bool horizontal)
    {
        TDimensions2 d = c.Dimensions_Get();
        return horizontal ? d.size.X : d.size.Y;
    }

    static float MinOf(ImpComp2D c, bool horizontal)
    {
        float min = horizontal ? c.size_min.X : c.size_min.Y;
        return min > 0 ? min : 48f;
    }

    static float MaxOf(ImpComp2D c, bool horizontal)
    {
        return horizontal ? c.size_max.X : c.size_max.Y;
    }

    static void SetMain(ImpComp2D c, bool horizontal, float v)
    {
        if (horizontal) c.size.X = v;
        else c.size.Y = v;
    }

    static void Cursor_Set(bool horizontal)
    {
        Raylib.SetMouseCursor(horizontal ? MouseCursor.ResizeEw : MouseCursor.ResizeNs);
    }

    static void Cursor_Clear()
    {
        Raylib.SetMouseCursor(MouseCursor.Default);
    }
}

public class UiStyle_Seperator : ImpAsset
{
    public static UiStyle_Seperator DEFAULT = new();

    [ImpVar] public A_Texture? image = A_Texture.SEPERATOR_V;
    [ImpVar] public Color tint = new(200, 200, 200, 255);
    [ImpVar] public Color tint_hovered = new(0, 120, 215, 255);
    [ImpVar] public Color tint_pressed = new(0, 84, 153, 255);
}
