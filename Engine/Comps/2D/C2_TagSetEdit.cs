using System.Numerics;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Hidden = true)]
public class C2_TagSetEdit : Imp2D
{
    const float HeadH = 26f;
    const float BtnW = 52f;

    public string label = "";
    public Func<TTagSet> value_get;
    public Action<TTagSet> value_set;

    public C2_Button c_btn_add;
    public C2_Button c_btn_clear;
    public C2_List c_rows;

    string _fp = "\0";

    public C2_TagSetEdit()
    {
        cursor_filter = ECursorFilter.Pass;
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Start;
        layout.size = new Vector2(0, HeadH);
        layout.size_min = layout.size;

        c_btn_add = new C2_Button
        {
            text = "Add",
            text_style = UI_Text.LIGHT,
            on_click = OpenPicker,
            layout = new TLayout2
            {
                size = new Vector2(BtnW, 22),
                size_min = new Vector2(BtnW, 22),
            },
        };
        Child_Add(c_btn_add);

        c_btn_clear = new C2_Button
        {
            text = "Clear",
            text_style = UI_Text.LIGHT,
            on_click = Clear,
            layout = new TLayout2
            {
                size = new Vector2(BtnW, 22),
                size_min = new Vector2(BtnW, 22),
            },
        };
        Child_Add(c_btn_clear);

        c_rows = new C2_List
        {
            orentation = EUIOrentation.V,
            spacing = 2,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Start,
            },
        };
        Child_Add(c_rows);
    }

    TTagSet Value
    {
        get
        {
            if (value_get != null)
            {
                return value_get();
            }
            return null;
        }
    }

    void Value_Set(TTagSet set)
    {
        value_set?.Invoke(set);
        _fp = "\0";
    }

    void OpenPicker()
    {
        TTagSet cur = Value;
        if (cur == null)
        {
            cur = new TTagSet();
        }
        Dialog_TagPicker.Run(cur, picked =>
        {
            if (picked == null)
            {
                Value_Set(new TTagSet());
                return;
            }
            Value_Set(picked.Clone());
        }, title: "Select Tags");
    }

    void Clear()
    {
        Value_Set(new TTagSet());
    }

    void Remove(TTag tag)
    {
        TTagSet cur = Value;
        TTagSet next = new TTagSet();
        if (cur != null)
        {
            next = cur.Clone();
        }
        next.RemoveTag(tag);
        Value_Set(next);
    }

    static string Fingerprint(TTagSet set)
    {
        if (set == null || set.IsEmpty)
        {
            return "";
        }
        List<TTag> tags = set.Sorted();
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < tags.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }
            sb.Append(tags[i].TagName);
        }
        return sb.ToString();
    }

    void Rows_Sync()
    {
        TTagSet set = Value;
        string fp = Fingerprint(set);
        if (fp == _fp)
        {
            return;
        }
        _fp = fp;
        c_rows.Child_RemoveAll();
        if (set == null || set.IsEmpty)
        {
            return;
        }
        List<TTag> tags = set.Sorted();
        for (int i = 0; i < tags.Count; i++)
        {
            TTag tag = tags[i];
            C2_TagRow row = new(tag, () => Remove(tag));
            c_rows.Child_Add(row);
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Rows_Sync();

        TDimensions2 dim = Dimensions_Get();
        bool has_tags = Value != null && !Value.IsEmpty;
        c_btn_clear.is_visible = has_tags;

        float right = BtnW;
        if (has_tags)
        {
            right = BtnW * 2 + 4;
            c_btn_clear.transform.position = new Vector2(dim.size.X - BtnW, 2);
            c_btn_clear.layout.orient_H = EUIViewportAlignment.Start;
            c_btn_clear.layout.orient_V = EUIViewportAlignment.Start;
        }
        c_btn_add.transform.position = new Vector2(dim.size.X - right, 2);
        c_btn_add.layout.orient_H = EUIViewportAlignment.Start;
        c_btn_add.layout.orient_V = EUIViewportAlignment.Start;

        float rows_h = 0;
        if (c_rows.children.Count > 0)
        {
            c_rows.is_visible = true;
            c_rows.transform.position = new Vector2(10, HeadH + 2);
            for (int i = 0; i < c_rows.children.Count; i++)
            {
                if (c_rows.children[i] is Imp2D d && d.is_visible)
                {
                    rows_h += d.layout.size.Y + 2;
                }
            }
            c_rows.layout.size = new Vector2(MathF.Max(0, dim.size.X - 10), rows_h);
            c_rows.layout.orient_H = EUIViewportAlignment.Start;
            c_rows.layout.orient_V = EUIViewportAlignment.Start;
        }
        else
        {
            c_rows.is_visible = false;
        }

        float h = HeadH;
        if (rows_h > 0)
        {
            h = HeadH + rows_h + 4;
        }
        layout.size = new Vector2(layout.size.X, h);
        layout.size_min = new Vector2(layout.size_min.X, layout.size.Y);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        TDimensions2 dim = Dimensions_Get();
        UI_Box.BkgMid.Draw(new TDimensions2 { position = dim.position, size = new Vector2(dim.size.X, HeadH) });
        UI_Text.LIGHT.Draw(label, new Vector2(dim.position.X + 8, dim.position.Y),
            new Vector2(MathF.Max(0, dim.size.X * 0.38f - 8), HeadH),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
        if (!c_rows.is_visible)
        {
            float lx = dim.position.X + dim.size.X * 0.38f;
            UI_Text.MUTED.Draw("None", new Vector2(lx, dim.position.Y),
                new Vector2(MathF.Max(0, dim.size.X - lx - BtnW - 8), HeadH),
                0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
        }
        base.OnDraw2D(dt, flags);
    }
}

[ImpClass(Hidden = true)]
class C2_TagRow : Imp2D
{
    public TTag tag;
    C2_Text _name;
    C2_Button _remove;

    public C2_TagRow(TTag tag, Action on_remove)
    {
        this.tag = tag;
        cursor_filter = ECursorFilter.Pass;
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, 22),
            size_min = new Vector2(0, 22),
        };

        _name = new C2_Text
        {
            text = tag.TagName ?? "",
            style = UI_Text.LIGHT,
            wrap = ETextWrap.None,
            text_alignment_h = EUIPositionAlignment.Start,
            text_alignment_v = EUIPositionAlignment.Center,
            cursor_filter = ECursorFilter.Ignore,
        };
        Child_Add(_name);

        _remove = new C2_Button
        {
            text = "x",
            text_style = UI_Text.MUTED,
            on_click = on_remove,
            layout = new TLayout2
            {
                size = new Vector2(18, 18),
                size_min = new Vector2(18, 18),
            },
        };
        Child_Add(_remove);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TDimensions2 dim = Dimensions_Get();
        _remove.transform.position = new Vector2(MathF.Max(0, dim.size.X - 20), 2);
        _remove.layout.orient_H = EUIViewportAlignment.Start;
        _remove.layout.orient_V = EUIViewportAlignment.Start;

        _name.transform.position = Vector2.Zero;
        _name.layout.size = new Vector2(MathF.Max(0, dim.size.X - 22), dim.size.Y);
        _name.layout.orient_H = EUIViewportAlignment.Start;
        _name.layout.orient_V = EUIViewportAlignment.Fill;
    }
}
