using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_Expandable : ImpComp2D
{
    public bool is_expanded = true;
    public float bar_height = 24f;
    public float content_indent = 10f;
    public Action<bool> on_expand;

    public C2_List list = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };
    public C2_List content_box = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_H = EUIViewportAlignment.Start,
        view_alighnment_V = EUIViewportAlignment.Fill,
        spacing = 2,
    };

    public UiStyle_Expandable style = UiStyle_Expandable.DEFAULT;

    C2_Button bar = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        layout = EButtonLayout.Icon_Text_H,
        content_align_h = EUIPositionAlignment.Start,
        style = new UiStyle_Button(),
    };

    bool _was_expanded = true;
    bool _shrunk;
    EUIViewportAlignment _saved_align_v;
    float _saved_size_y;

    public C2_Expandable()
    {
        cursor_filter = ECursorFilter.Pass;
        Child_Add(list);
        list.Child_Add(bar);
        list.Child_Add(content_box);
        bar.on_click = () => { is_expanded = !is_expanded; };
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        if (!children.Contains(list))
            Child_Add(list);
        if (!list.children.Contains(bar))
            list.Child_Add(bar);
        if (!list.children.Contains(content_box))
            list.Child_Add(content_box);

        for (int i = 0; i < children.Count; )
        {
            ImpComp c = children[i];
            if (c == list) { i++; continue; }
            children.RemoveAt(i);
            c.parent = null;
            content_box.Child_Add(c);
        }

        for (int i = 0; i < list.children.Count; )
        {
            ImpComp c = list.children[i];
            if (c == bar || c == content_box) { i++; continue; }
            list.children.RemoveAt(i);
            c.parent = null;
            content_box.Child_Add(c);
        }

        if (style != null)
        {
            bar.text_style = style.name_style;
            bar.style.style_unhovered = style.style_expand_bar;
            bar.style.style_hovered = style.style_expand_bar;
            bar.style.style_pressed = style.style_expand_bar;
        }

        bar.icon = is_expanded ? style?.icon_expand : style?.icon_collapse;
        bar.text = name ?? "";
        bar.size = new Vector2(bar.size.X, bar_height);
        bar.size_min = new Vector2(0, bar_height);

        content_box.is_visible = is_expanded;
        if (content_indent > 0)
        {
            content_box.view_alighnment_H = EUIViewportAlignment.Start;
            TDimensions2 dim = Dimensions_Get();
            content_box.size = new Vector2(MathF.Max(0, dim.size.X - content_indent), content_box.size.Y);
            content_box.transform.position = new Vector2(content_indent, content_box.transform.position.Y);
        }

        if (!is_expanded)
        {
            if (!_shrunk)
            {
                _saved_align_v = view_alighnment_V;
                _saved_size_y = size.Y;
                _shrunk = true;
            }
            view_alighnment_V = EUIViewportAlignment.Start;
            size.Y = bar_height;
            size_min.Y = bar_height;
        }
        else
        {
            if (_shrunk)
            {
                view_alighnment_V = _saved_align_v;
                size.Y = _saved_size_y;
                _shrunk = false;
            }
            if (view_alighnment_V != EUIViewportAlignment.Fill)
            {
                float inner = 0;
                int vis = 0;
                List<ImpComp> kids = content_box.scroll_box != null ? content_box.scroll_box.children : content_box.children;
                for (int i = 0; i < kids.Count; i++)
                {
                    if (kids[i] is not ImpComp2D d || !d.is_visible) continue;
                    float h = d.size_min.Y > 0 ? d.size_min.Y : d.size.Y;
                    inner += h;
                    vis++;
                }
                if (vis > 1) inner += content_box.spacing * (vis - 1);
                size.Y = bar_height + inner;
                size_min.Y = size.Y;
            }
        }

        if (_was_expanded != is_expanded)
        {
            _was_expanded = is_expanded;
            on_expand?.Invoke(is_expanded);
        }
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (!is_expanded || style?.style_content_box == null) return;
        TDimensions2 dim = Dimensions_Get();
        style.style_content_box.Draw(new TDimensions2
        {
            position = dim.position + new Vector2(0, bar_height),
            size = new Vector2(dim.size.X, MathF.Max(0, dim.size.Y - bar_height)),
        });
        if (content_indent > 2)
        {
            Raylib.DrawRectangleV(
                dim.position + new Vector2(content_indent * 0.35f, bar_height + 3),
                new Vector2(2, MathF.Max(0, dim.size.Y - bar_height - 6)),
                new Color(255, 255, 255, 22));
        }
    }
}

public class UiStyle_Expandable : ImpAsset
{
    public static UiStyle_Expandable DEFAULT = new();
    
    [ImpVar] public A_Texture icon_collapse = A_Texture.ICO_ARROW_R;
    [ImpVar] public A_Texture icon_expand = A_Texture.ICO_ARROW_D;
    [ImpVar] public UiStyle_Box style_expand_bar = UiStyle_Box.STYLE_BKG_MID;
    [ImpVar] public UiStyle_Box style_content_box = UiStyle_Box.STYLE_BKG_DARK;
    [ImpVar] public UiStyle_Text name_style = UiStyle_Text.LIGHT;
    
}