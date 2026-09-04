using System.Collections;
using System.Numerics;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Comps._2D;

[ImpClass(Common = true)]
//displays a layed out list of children of a preset type with a data var for a source reference. callbacks for children being hovered/selected/highlighted/etc
public class C2_OptionList : Imp2D
{
    [ImpVar] public TClass<Imp2D> child_type = new(typeof(C2_Button));
    [ImpVar] public EUIOrentation orientation;
    [ImpVar] public TLayout2 child_layout = TLayout2.H_BAR;
    [ImpVar] public OptionListStyle list_style = new OptionListStyle_List();
    [ImpVar] public List<ImpAsset> default_options;
    
    public object list_context;
    public int selected_index = -1;
    public int hovered_index = -1;
    public int highlighted_index = -1;
    
    public Action<Imp2D,int> on_option_added;
    public Action<Imp2D,int> on_option_select;
    public Action<Imp2D,int,bool> on_option_hover;
    public Action<Imp2D,int,bool> on_option_highlight;

    Dictionary<Imp2D, Bound> _bound;

    struct Bound
    {
        public Action<C2_Button> click;
        public Action<C2_Button> hover;
        public Action<C2_Button> unhover;
    }

    public override bool Allow_Children() { return false; }
    public override bool ChildLayout_IsFree() { return false; }

    public override void OnInit()
    {
        base.OnInit();
        RebuildDefaults();
    }

    protected override void Transform_Refresh()
    {
        BindPending();
        if (scene != null && !scene.is_running)
            RebuildDefaultsIfStale();
        TLayout2 lay = ChildLayout_Effective();
        for (int i = 0; i < children.Count; i++)
            if (children[i] is Imp2D c) c.layout = lay;
        base.Transform_Refresh();
    }

    public Imp2D Option_Add(object data)
    {
        Type type = child_type.Get() ?? typeof(C2_Button);
        Imp2D _new_opt = Activator.CreateInstance(type) as Imp2D;
        if (_new_opt == null) return null;
        _new_opt.layout = ChildLayout_Effective();
        _new_opt.option_data = data;
        list_style?.OnAdded(this, _new_opt, children.Count);
        Child_Add(_new_opt, true);
        BindOption(_new_opt);
        _new_opt.OnOption_Added(this, children.Count - 1);
        _new_opt.Option_Refresh();
        on_option_added?.Invoke(_new_opt, children.Count - 1);
        return _new_opt;
    }

    public List<Imp2D> Option_AddList(List<object> data)
    {
        List<Imp2D> r = new();
        if (data == null) return r;
        foreach (object d in data)
        {
            Imp2D o = Option_Add(d);
            if (o != null) r.Add(o);
        }
        return r;
    }

    public void Option_Set(IList data)
    {
        Option_Clear();
        if (data == null) return;
        foreach (object d in data) Option_Add(d);
    }

    public void Option_Remove(Imp2D option)
    {
        if (option == null || option.parent != this) return;
        int i = children.IndexOf(option);
        UnbindOption(option);
        Child_Remove(option);
        ShiftIndex(ref selected_index, i);
        ShiftIndex(ref hovered_index, i);
        ShiftIndex(ref highlighted_index, i);
    }

    public void Option_RemoveByIndex(int index)
    {
        if (index < 0 || index >= children.Count) return;
        if (children[index] is Imp2D opt) Option_Remove(opt);
    }

    public void Option_Clear()
    {
        for (int i = children.Count - 1; i >= 0; i--)
            if (children[i] is Imp2D opt) Option_Remove(opt);
    }

    public void Option_Select(Imp2D opt)
    {
        int i = children.IndexOf(opt);
        if (i < 0) return;
        Option_HighlightByIndex(i);
        selected_index = i;
        on_option_select?.Invoke(opt, i);
        Hooks.opt_select?.Invoke(this, i, opt);
    }

    public void Option_Hover(Imp2D opt, bool hover)
    {
        int i = children.IndexOf(opt);
        if (i < 0) return;
        hovered_index = hover ? i : (hovered_index == i ? -1 : hovered_index);
        on_option_hover?.Invoke(opt, i, hover);
        if (hover) Hooks.opt_hover?.Invoke(this, i, opt);
        else Hooks.opt_unhover?.Invoke(this, i, opt);
    }

    public void Option_SelectByIndex(int index)
    {
        if (index < 0 || index >= children.Count) return;
        if (children[index] is Imp2D opt) Option_Select(opt);
    }

    public void Option_HighlightByIndex(int index)
    {
        int prev = highlighted_index;
        highlighted_index = index;
        if (prev >= 0 && prev < children.Count && prev != index && children[prev] is Imp2D a)
            on_option_highlight?.Invoke(a, prev, false);
        if (index >= 0 && index < children.Count && children[index] is Imp2D b)
            on_option_highlight?.Invoke(b, index, true);
    }

    public override TBounds2 Child_MakeBounds2D(ImpComp child, int index)
    {
        Vector2 PrefSize(Imp2D c)
        {
            Vector2 size = c.layout.size;
            if (c.layout.size_max != Vector2.Zero)
                size = Vector2.Clamp(size, c.layout.size_min, c.layout.size_max);
            else
                size = Vector2.Max(size, c.layout.size_min);
            return size;
        }

        TBounds2 Place(ImpComp c, TBounds2 slot)
        {
            // Return the slot only. Imp2D.OnDraw2D applies the child's layout.
            return c is Imp2D ? slot : default;
        }

        TBounds2 inner = ContentBounds();
        bool horiz = orientation == EUIOrentation.H;
        float inner_main = horiz
            ? MathF.Abs(inner.end.X - inner.start.X)
            : MathF.Abs(inner.end.Y - inner.start.Y);

        float preferred_total = 0;
        int fill_n = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not Imp2D c || !c.is_visible) continue;
            Vector2 sz = PrefSize(c);
            preferred_total += horiz ? sz.X : sz.Y;
            if ((horiz ? c.layout.alignment.align_H : c.layout.alignment.align_V) == ELayoutAlignment.Fill)
                fill_n++;
        }

        float extra = inner_main - preferred_total;
        if (extra < 0) extra = 0;
        float extra_each = fill_n > 0 ? extra / fill_n : 0;

        float x0 = MathF.Min(inner.start.X, inner.end.X);
        float y0 = MathF.Min(inner.start.Y, inner.end.Y);
        float x1 = MathF.Max(inner.start.X, inner.end.X);
        float y1 = MathF.Max(inner.start.Y, inner.end.Y);
        float cursor = 0;

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not Imp2D c || !c.is_visible) continue;
            Vector2 sz = PrefSize(c);
            float main = horiz ? sz.X : sz.Y;
            bool fill = (horiz ? c.layout.alignment.align_H : c.layout.alignment.align_V) == ELayoutAlignment.Fill;
            if (fill) main += extra_each;
            if (c.layout.size_max != Vector2.Zero)
            {
                float max = horiz ? c.layout.size_max.X : c.layout.size_max.Y;
                if (max > 0 && main > max) main = max;
            }

            TBounds2 slot = horiz
                ? new TBounds2 { start = new Vector2(x0 + cursor, y0), end = new Vector2(x0 + cursor + main, y1) }
                : new TBounds2 { start = new Vector2(x0, y0 + cursor), end = new Vector2(x1, y0 + cursor + main) };

            if (c == child) return Place(child, slot);
            cursor += main;
        }
        return default;
    }

    TLayout2 ChildLayout_Effective()
    {
        TLayout2 lay = child_layout;
        bool horiz = orientation == EUIOrentation.H;
        if (lay.size.X <= 0)
        {
            lay.size.X = horiz ? 100 : 20;
            if (!horiz) lay.alignment.align_H = ELayoutAlignment.Fill;
        }
        if (lay.size.Y <= 0)
        {
            lay.size.Y = horiz ? 20 : 32;
            if (horiz) lay.alignment.align_V = ELayoutAlignment.Fill;
        }
        return lay;
    }

    void RebuildDefaults()
    {
        Option_Clear();
        if (default_options == null) return;
        foreach (ImpAsset a in default_options)
            Option_Add(a);
    }

    void RebuildDefaultsIfStale()
    {
        int n = default_options?.Count ?? 0;
        if (children.Count != n) { RebuildDefaults(); return; }
        Type want = child_type.Get() ?? typeof(C2_Button);
        for (int i = 0; i < n; i++)
        {
            if (children[i] is not Imp2D opt) { RebuildDefaults(); return; }
            if (opt.GetType() != want || !ReferenceEquals(opt.option_data, default_options[i]))
            { RebuildDefaults(); return; }
        }
    }

    void BindPending()
    {
        for (int i = 0; i < children.Count; i++)
            if (children[i] is Imp2D opt) BindOption(opt);
    }

    void BindOption(Imp2D opt)
    {
        if (opt.option_button == null) return;
        if (_bound != null && _bound.ContainsKey(opt)) return;
        C2_Button btn = opt.option_button;
        Bound b = new()
        {
            click = _ => Option_Select(opt),
            hover = _ => Option_Hover(opt, true),
            unhover = _ => Option_Hover(opt, false),
        };
        btn.on_click += b.click;
        btn.on_hover += b.hover;
        btn.on_unhover += b.unhover;
        _bound ??= new();
        _bound[opt] = b;
    }

    void UnbindOption(Imp2D opt)
    {
        if (_bound == null || !_bound.Remove(opt, out Bound b)) return;
        C2_Button btn = opt.option_button;
        if (btn == null) return;
        btn.on_click -= b.click;
        btn.on_hover -= b.hover;
        btn.on_unhover -= b.unhover;
    }

    static void ShiftIndex(ref int index, int removed)
    {
        if (index == removed) index = -1;
        else if (index > removed) index--;
    }
    
    
}

public class OptionListStyle : ImpAsset
{
    public virtual void OnAdded(C2_OptionList list, Imp2D option, int index) { }
    //public virtual void OnRefresh(C2_OptionList list, Imp2D option, int index) { }
}

public class OptionListStyle_List : OptionListStyle
{
    [ImpVar] public EUIOrentation orientation;
}

public class OptionListStyle_ScrollList : OptionListStyle
{
    [ImpVar] public EUIOrentation orientation;
    [ImpVar] public UI_ScrollBox scroll_style;
}

public class OptionListStyle_Carousel : OptionListStyle
{
    [ImpVar] public EUIOrentation orientation;
    [ImpVar] public UI_ScrollBox scroll_style;
}
