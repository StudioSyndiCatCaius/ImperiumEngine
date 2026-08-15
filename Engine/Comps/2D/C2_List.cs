using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Common = true)]
public class C2_List : Imp2D
{
    public EUIOrentation orentation = EUIOrentation.V;
    public bool is_scrollable = false;
    public float spacing = 0;
    
    public int section_count = 0; //if >0, this will be the number of sections to display before starting a new set (E.G. if orentation=H, this will be the number of columns before starting a new row)
    public bool auto_scale_section_count = false; //if true, will automatically scale the section based on dimensions of self and children

    public bool override_child_alignment_h = false;
    public EUIViewportAlignment child_alignment_h = EUIViewportAlignment.Fill;
    public bool override_child_alignment_v = false;
    public EUIViewportAlignment child_alignment_v = EUIViewportAlignment.Fill;

    public Action<Imp2D, int> on_option_select;
    public Action<Imp2D, int> on_option_hover;
    public Action<Imp2D, int> on_option_unhover;

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
                    orentation = orentation,
                    spacing = spacing,
                    layout = new TLayout2
                        {
                            orient_H = EUIViewportAlignment.Fill,
                            orient_V = EUIViewportAlignment.Fill,
                        },
        };
                Child_Add(scroll_box);
            }

            scroll_box.orentation = orentation;
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
                bool horizontal = orentation == EUIOrentation.H;
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
            if (kids[i] is not Imp2D child || !child.is_visible) continue;
            visible++;
            bool fill = child is not C2_Seperator && (horizontal
                ? child.layout.orient_H == EUIViewportAlignment.Fill
                : child.layout.orient_V == EUIViewportAlignment.Fill);
            if (fill)
            {
                stretch[i] = true;
                stretch_sum += MathF.Max(0f, child.stretch_ratio);
            }
            else
                fixed_sum += horizontal ? child.layout.size.X : child.layout.size.Y;
        }

        float remaining = available - fixed_sum - (visible > 1 ? spacing * (visible - 1) : 0);

        bool frozen = true;
        while (frozen)
        {
            frozen = false;
            float unit = stretch_sum > 0 && remaining > 0 ? remaining / stretch_sum : 0;
            for (int i = 0; i < n; i++)
            {
                if (!stretch[i] || kids[i] is not Imp2D child) continue;
                float ratio = MathF.Max(0f, child.stretch_ratio);
                float main = unit * ratio;
                float min = horizontal ? child.layout.size_min.X : child.layout.size_min.Y;
                float max = horizontal ? child.layout.size_max.X : child.layout.size_max.Y;
                if (min > 0 && main < min) main = min;
                else if (max > 0 && remaining > 0 && main > max) main = max;
                else continue;

                stretch[i] = false;
                stretch_sum -= ratio;
                remaining -= main;
                if (horizontal) child.layout.size = new Vector2(main, child.layout.size.Y);
                else child.layout.size = new Vector2(child.layout.size.X, main);
                frozen = true;
                break;
            }
        }
        if (remaining < 0) remaining = 0;

        float axis = -scroll;
        for (int i = 0; i < n; i++)
        {
            if (kids[i] is not Imp2D child || !child.is_visible) continue;

            float main;
            if (stretch[i])
            {
                float ratio = MathF.Max(0f, child.stretch_ratio);
                main = stretch_sum > 0 ? remaining * (ratio / stretch_sum) : 0;
                if (horizontal) child.layout.size = new Vector2(main, child.layout.size.Y);
                else child.layout.size = new Vector2(child.layout.size.X, main);
            }
            else
            {
                main = horizontal ? child.layout.size.X : child.layout.size.Y;
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
        bool horizontal = orentation == EUIOrentation.H;

        float cell_w = 0, cell_h = 0;
        int visible = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not Imp2D child || !child.is_visible) continue;
            float iw = child.layout.size_min.X > 0 ? child.layout.size_min.X : child.layout.size.X;
            float ih = child.layout.size_min.Y > 0 ? child.layout.size_min.Y : child.layout.size.Y;
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
            if (children[i] is not Imp2D child || !child.is_visible) continue;
            int col = index % cols;
            int row = index / cols;
            if (horizontal)
            {
                child.layout.size = new Vector2(cell_w, child.layout.size.Y);
                child.transform.position = new Vector2(col * (cell_w + spacing), row * (cell_h + spacing));
            }
            else
            {
                child.layout.size = new Vector2(child.layout.size.X, cell_h);
                child.transform.position = new Vector2(row * (cell_w + spacing), col * (cell_h + spacing));
            }
            index++;
        }
    }

    void ApplyChildOverrides(List<ImpComp> kids)
    {
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] is not Imp2D d || d is C2_Seperator) continue;
            if (override_child_alignment_h) d.layout.orient_H = child_alignment_h;
            if (override_child_alignment_v) d.layout.orient_V = child_alignment_v;
        }
    }

    void BindOptions(List<ImpComp> kids)
    {
        int index = 0;
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] is not Imp2D d || d is C2_Seperator) continue;
            Imp2D target = d.option_button ?? d;
            int captured = index;
            Imp2D item = d;
            target.as_option_select = _ => on_option_select?.Invoke(item, captured);
            target.as_option_hover = _ => on_option_hover?.Invoke(item, captured);
            target.as_option_unhover = _ => on_option_unhover?.Invoke(item, captured);
            index++;
        }
    }
}


public class UI_List : ImpAsset
{
    [ImpVar] public UiStyle_Box box_background = null;

    public static UI_List DEFAULT = new();
}
