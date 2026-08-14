using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._2D;

public class C2_List : ImpComp2D
{
    public EUIAlignment alignment = EUIAlignment.Vertical;
    public bool is_scrollable = false;
    public float spacing = 0;
    
    public int section_count = 0; //if >0, this will be the number of sections to display before starting a new set (E.G. if alignment=Horizontal, this will be the number of columns before starting a new row)
    public bool auto_scale_section_count = false; //if true, will automatically scale the section based on dimensions of self and children

    public bool override_child_alignment_h = false;
    public EUIViewportAlignment child_alignment_h = EUIViewportAlignment.Fill;
    public bool override_child_alignment_v = false;
    public EUIViewportAlignment child_alignment_v = EUIViewportAlignment.Fill;

    public Action<ImpComp2D, int> on_option_select;
    public Action<ImpComp2D, int> on_option_hover;
    public Action<ImpComp2D, int> on_option_unhover;

    public C2_ScrollBox scroll_box;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        cursor_filter = ECursorFilter.Pass;

        if (is_scrollable)
        {
            if (scroll_box != null && scroll_box.parent != this)
                scroll_box = null;
            if (scroll_box == null)
            {
                scroll_box = new C2_ScrollBox
                {
                    view_alighnment_H = EUIViewportAlignment.Fill,
                    view_alighnment_V = EUIViewportAlignment.Fill,
                    alignment = alignment,
                    spacing = spacing,
                };
                Child_Add(scroll_box);
            }

            scroll_box.alignment = alignment;
            scroll_box.spacing = spacing;
            scroll_box.section_count = section_count;
            scroll_box.auto_scale_section_count = auto_scale_section_count;
            scroll_box.is_visible = true;

            for (int i = children.Count - 1; i >= 0; i--)
            {
                ImpComp c = children[i];
                if (c == scroll_box) continue;
                children.RemoveAt(i);
                c.parent = null;
                scroll_box.Child_Add(c);
            }

            ApplyChildOverrides(scroll_box.children);
            BindOptions(scroll_box.children);
        }
        else
        {
            if (scroll_box != null)
            {
                for (int i = scroll_box.children.Count - 1; i >= 0; i--)
                {
                    ImpComp c = scroll_box.children[i];
                    scroll_box.children.RemoveAt(i);
                    c.parent = null;
                    Child_Add(c);
                }
                children.Remove(scroll_box);
                scroll_box.parent = null;
                scroll_box = null;
            }

            ApplyChildOverrides(children);
            if (section_count > 0 || auto_scale_section_count) LayoutWrap();
            else
            {
                TDimensions2 dim = Dimensions_Get();
                bool horizontal = alignment == EUIAlignment.Horizontal;
                LayoutMainAxis(children, horizontal, horizontal ? dim.size.X : dim.size.Y, spacing);
            }
            BindOptions(children);
        }
    }

    public static float LayoutMainAxis(List<ImpComp> kids, bool horizontal, float available, float spacing, float scroll = 0)
    {
        int n = kids.Count;
        int visible = 0;
        float fixed_sum = 0;
        float stretch_sum = 0;
        bool[] stretch = new bool[n];

        for (int i = 0; i < n; i++)
        {
            if (kids[i] is not ImpComp2D child || !child.is_visible) continue;
            visible++;
            bool fill = child is not C2_Seperator && (horizontal
                ? child.view_alighnment_H == EUIViewportAlignment.Fill
                : child.view_alighnment_V == EUIViewportAlignment.Fill);
            if (fill)
            {
                stretch[i] = true;
                stretch_sum += MathF.Max(0f, child.stretch_ratio);
            }
            else
                fixed_sum += horizontal ? child.size.X : child.size.Y;
        }

        float remaining = available - fixed_sum - (visible > 1 ? spacing * (visible - 1) : 0);

        bool frozen = true;
        while (frozen)
        {
            frozen = false;
            float unit = stretch_sum > 0 && remaining > 0 ? remaining / stretch_sum : 0;
            for (int i = 0; i < n; i++)
            {
                if (!stretch[i] || kids[i] is not ImpComp2D child) continue;
                float ratio = MathF.Max(0f, child.stretch_ratio);
                float main = unit * ratio;
                float min = horizontal ? child.size_min.X : child.size_min.Y;
                float max = horizontal ? child.size_max.X : child.size_max.Y;
                if (min > 0 && main < min) main = min;
                else if (max > 0 && remaining > 0 && main > max) main = max;
                else continue;

                stretch[i] = false;
                stretch_sum -= ratio;
                remaining -= main;
                if (horizontal) child.size = new Vector2(main, child.size.Y);
                else child.size = new Vector2(child.size.X, main);
                frozen = true;
                break;
            }
        }
        if (remaining < 0) remaining = 0;

        float axis = -scroll;
        for (int i = 0; i < n; i++)
        {
            if (kids[i] is not ImpComp2D child || !child.is_visible) continue;

            float main;
            if (stretch[i])
            {
                float ratio = MathF.Max(0f, child.stretch_ratio);
                main = stretch_sum > 0 ? remaining * (ratio / stretch_sum) : 0;
                if (horizontal) child.size = new Vector2(main, child.size.Y);
                else child.size = new Vector2(child.size.X, main);
            }
            else
            {
                main = horizontal ? child.size.X : child.size.Y;
            }

            if (horizontal)
                child.transform.position = new Vector2(axis, child.transform.position.Y);
            else
                child.transform.position = new Vector2(child.transform.position.X, axis);

            axis += main + spacing;
        }

        return visible > 0 ? axis + scroll - spacing : 0;
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
        if (visible == 0 || cell_w <= 0 || cell_h <= 0) return;

        float avail = horizontal ? dim.size.X : dim.size.Y;
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
                child.transform.position = new Vector2(col * (cell_w + spacing), row * (cell_h + spacing));
            }
            else
            {
                child.size = new Vector2(child.size.X, cell_h);
                child.transform.position = new Vector2(row * (cell_w + spacing), col * (cell_h + spacing));
            }
            index++;
        }
    }

    void ApplyChildOverrides(List<ImpComp> kids)
    {
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] is not ImpComp2D d || d is C2_Seperator) continue;
            if (override_child_alignment_h) d.view_alighnment_H = child_alignment_h;
            if (override_child_alignment_v) d.view_alighnment_V = child_alignment_v;
        }
    }

    void BindOptions(List<ImpComp> kids)
    {
        int index = 0;
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] is not ImpComp2D d || d is C2_Seperator) continue;
            ImpComp2D target = d.option_button ?? d;
            int captured = index;
            ImpComp2D item = d;
            target.as_option_select = _ => on_option_select?.Invoke(item, captured);
            target.as_option_hover = _ => on_option_hover?.Invoke(item, captured);
            target.as_option_unhover = _ => on_option_unhover?.Invoke(item, captured);
            index++;
        }
    }
}
