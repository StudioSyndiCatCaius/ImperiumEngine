using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_ScrollBox : C2_Box
{
    public EUIAlignment alignment = EUIAlignment.Vertical;
    public float scroll = 0;
    public float scroll_step = 40f;
    public float spacing = 0;
    public float content_length;

    public int section_count = 0;
    public bool auto_scale_section_count = false;

    public UiStyle_ScrollBox scroll_style = UiStyle_ScrollBox.DEFAULT;
    public float scrollbar_thickness = 10f;

    bool _bar_drag;
    float _bar_grab;

    public C2_ScrollBox()
    {
        clip_children = true;
    }

    public bool IsWrapped => section_count > 0 || auto_scale_section_count;

    public EUIAlignment ScrollAxis =>
        IsWrapped
            ? (alignment == EUIAlignment.Horizontal ? EUIAlignment.Vertical : EUIAlignment.Horizontal)
            : alignment;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        cursor_filter = ECursorFilter.Pass;

        if (IsWrapped) LayoutWrap();
        else LayoutLinear();

        TDimensions2 dim = Dimensions_Get();
        bool scroll_v = ScrollAxis == EUIAlignment.Vertical;
        float view_len = scroll_v ? dim.size.Y : dim.size.X;
        float max_scroll = MathF.Max(0, content_length - view_len);

        HandleBar(dim, view_len, max_scroll, scroll_v);

        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.Cursor_IsInDimensions(dim) || _bar_drag)
            {
                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0)
                    scroll -= wheel * scroll_step;
            }
        }

        scroll = Math.Clamp(scroll, 0, max_scroll);
    }

    void HandleBar(TDimensions2 dim, float view_len, float max_scroll, bool scroll_v)
    {
        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer p = ImpPlayer.players[0];
        Vector2 m = p.cursor.position;

        if (content_length <= view_len || view_len <= 0)
        {
            if (_bar_drag)
            {
                _bar_drag = false;
                if (p.input_hog == this) p.input_hog = null;
            }
            return;
        }

        BarMetrics(dim, view_len, max_scroll, scroll_v,
            out Rectangle track, out Rectangle bar, out float track_travel);

        bool on_bar = m.X >= bar.X && m.X < bar.X + bar.Width && m.Y >= bar.Y && m.Y < bar.Y + bar.Height;
        bool on_track = m.X >= track.X && m.X < track.X + track.Width && m.Y >= track.Y && m.Y < track.Y + track.Height;

        if (_bar_drag)
        {
            if (!ImpPlayer.Key_IsDown(EInputKey.Mouse_Left))
            {
                _bar_drag = false;
                if (p.input_hog == this) p.input_hog = null;
            }
            else if (track_travel > 0)
            {
                float pos = scroll_v ? m.Y : m.X;
                float start = scroll_v ? dim.position.Y : dim.position.X;
                scroll = Math.Clamp((pos - _bar_grab - start) / track_travel, 0, 1) * max_scroll;
            }
            return;
        }

        if (!ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left)) return;
        if (!on_bar && !on_track) return;

        float bar_len = scroll_v ? bar.Height : bar.Width;
        if (on_bar)
            _bar_grab = (scroll_v ? m.Y - bar.Y : m.X - bar.X);
        else
        {
            _bar_grab = bar_len * 0.5f;
            float pos = scroll_v ? m.Y : m.X;
            float start = scroll_v ? dim.position.Y : dim.position.X;
            if (track_travel > 0)
                scroll = Math.Clamp((pos - _bar_grab - start) / track_travel, 0, 1) * max_scroll;
        }
        _bar_drag = true;
        p.input_hog = this;
    }

    void BarMetrics(TDimensions2 dim, float view_len, float max_scroll, bool scroll_v,
        out Rectangle track, out Rectangle bar, out float track_travel)
    {
        float thick = scrollbar_thickness;
        float ratio = view_len / content_length;
        float bar_len = MathF.Max(16, view_len * ratio);
        float t = max_scroll > 0 ? scroll / max_scroll : 0;
        track_travel = MathF.Max(0, view_len - bar_len);
        if (scroll_v)
        {
            track = new Rectangle(dim.position.X + dim.size.X - thick, dim.position.Y, thick, dim.size.Y);
            bar = new Rectangle(track.X, dim.position.Y + track_travel * t, thick, bar_len);
        }
        else
        {
            track = new Rectangle(dim.position.X, dim.position.Y + dim.size.Y - thick, dim.size.X, thick);
            bar = new Rectangle(dim.position.X + track_travel * t, track.Y, bar_len, thick);
        }
    }

    void LayoutLinear()
    {
        TDimensions2 dim = Dimensions_Get();
        bool horizontal = alignment == EUIAlignment.Horizontal;
        content_length = C2_List.LayoutMainAxis(
            children, horizontal, horizontal ? dim.size.X : dim.size.Y, spacing, scroll);
    }

    void LayoutWrap()
    {
        TDimensions2 dim = Dimensions_Get();
        bool horizontal = alignment == EUIAlignment.Horizontal;

        float cell_w = 0, cell_h = 0;
        int visible = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not ImpComp2D child || !child.is_visible) continue;
            float iw = child.size_min.X > 0 ? child.size_min.X : child.size.X;
            float ih = child.size_min.Y > 0 ? child.size_min.Y : child.size.Y;
            cell_w = MathF.Max(cell_w, iw);
            cell_h = MathF.Max(cell_h, ih);
            visible++;
        }

        if (visible == 0 || cell_w <= 0 || cell_h <= 0)
        {
            content_length = 0;
            return;
        }

        float gutter = scrollbar_thickness + 2;
        float avail = (horizontal ? dim.size.X : dim.size.Y) - gutter;
        if (avail < 1) avail = 1;
        int cols;
        if (auto_scale_section_count)
        {
            float step = (horizontal ? cell_w : cell_h) + spacing;
            cols = step > 0 ? Math.Max(1, (int)((avail + spacing) / step)) : 1;
        }
        else cols = Math.Max(1, section_count);

        if (horizontal) cell_w = (avail - (cols - 1) * spacing) / cols;
        else cell_h = (avail - (cols - 1) * spacing) / cols;

        int index = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not ImpComp2D child || !child.is_visible) continue;
            int col = index % cols;
            int row = index / cols;
            if (horizontal)
            {
                child.size = new Vector2(cell_w, child.size.Y);
                child.transform.position = new Vector2(col * (cell_w + spacing), row * (cell_h + spacing) - scroll);
            }
            else
            {
                child.size = new Vector2(child.size.X, cell_h);
                child.transform.position = new Vector2(row * (cell_w + spacing) - scroll, col * (cell_h + spacing));
            }
            index++;
        }

        int rows = (visible + cols - 1) / cols;
        float cross = horizontal ? cell_h : cell_w;
        content_length = rows > 0 ? rows * (cross + spacing) - spacing : 0;
    }

    public override void OnDraw2DForeground(double dt, WDrawFlags flags)
    {
        base.OnDraw2DForeground(dt, flags);

        TDimensions2 dim = Dimensions_Get();
        bool scroll_v = ScrollAxis == EUIAlignment.Vertical;
        float view_len = scroll_v ? dim.size.Y : dim.size.X;
        if (content_length <= view_len || view_len <= 0 || scroll_style == null) return;

        float max_scroll = content_length - view_len;
        BarMetrics(dim, view_len, max_scroll, scroll_v, out Rectangle track, out Rectangle bar, out _);

        scroll_style.scrollbar_background.Draw(new TDimensions2
        {
            position = new Vector2(track.X, track.Y),
            size = new Vector2(track.Width, track.Height)
        });
        scroll_style.scrollbar_bar.Draw(new TDimensions2
        {
            position = new Vector2(bar.X, bar.Y),
            size = new Vector2(bar.Width, bar.Height)
        });
    }
}

public class UiStyle_ScrollBox : ImpAsset
{
    public static UiStyle_ScrollBox DEFAULT = new();

    [ImpVar] public UiStyle_Box scrollbar_bar = UiStyle_Box.STYLE_BTN_IDLE;
    [ImpVar] public UiStyle_Box scrollbar_background = UiStyle_Box.STYLE_BKG_DARK;
}
