using System.Numerics;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Hidden = true)]
public class C2_PopupMenu : C2_Box
{
    public C2_SearchBar search;
    public C2_List list = new();

    public float row_height = 24f;
    public float menu_width = 200f;
    public float search_height = 26f;
    public float max_height = 420f;

    public Action<int> on_pick;
    public Action on_escape;

    static readonly Color ColBg = new(36, 36, 36, 255);
    static readonly Color ColHover = new(0, 96, 166, 255);
    static readonly Color ColPress = new(0, 70, 130, 255);

    public C2_PopupMenu()
    {
        style = new UI_Box { tint = ColBg };
        cursor_filter = ECursorFilter.Hit;
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Start,
            orient_V = EUIViewportAlignment.Start,
        };

        list.orentation = EUIOrentation.V;
        list.spacing = 0;
        list.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
        };
        Child_Add(list);
    }

    public void Search_SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (search != null)
            {
                return;
            }
            search = new C2_SearchBar
            {
                placeholder = "Search",
                layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Start,
                    size = new Vector2(0, search_height),
                    size_min = new Vector2(0, search_height),
                },
            };
            Child_Insert(0, search);
            return;
        }
        if (search == null)
        {
            return;
        }
        search.Destroy();
        search = null;
    }

    public void Fill(List<TPopupMenuOption> options)
    {
        if (list.scroll_box != null)
        {
            list.scroll_box.Child_RemoveAll();
        }
        else
        {
            list.Child_RemoveAll();
        }
        list.is_scrollable = false;

        if (options == null)
        {
            options = new List<TPopupMenuOption>();
        }

        float total_h = 4f;
        for (int i = 0; i < options.Count; i++)
        {
            TPopupMenuOption opt = options[i];
            if (opt.is_separator)
            {
                Imp2D sep = new()
                {
                    layout = new TLayout2
                    {
                        size = new Vector2(menu_width, 6),
                    },
                    cursor_filter = ECursorFilter.Ignore,
                };
                list.Child_Add(sep);
                total_h += 6;
                continue;
            }

            int captured = i;
            bool has_sub = opt.suboptions != null && opt.suboptions.Count > 0;
            string label = opt.text ?? "";
            if (has_sub)
            {
                label = label + "  >";
            }
            C2_Button btn = new()
            {
                text = label,
                layout = new TLayout2
                {
                    size = new Vector2(menu_width, row_height),
                    size_min = new Vector2(menu_width, row_height),
                },
                is_disabled = opt.is_disabled,
                content_align_h = EUIPositionAlignment.Start,
                content_pad = 8,
                text_style = UI_Text.LIGHT,
                style = new UI_Button
                {
                    unhovered = new UI_Box { tint = ColBg },
                    hovered = new UI_Box { tint = ColHover },
                    pressed = new UI_Box { tint = ColPress },
                },
            };
            btn.on_click = () =>
            {
                if (on_pick != null)
                {
                    on_pick(captured);
                }
            };
            list.Child_Add(btn);
            total_h += row_height;
        }

        float search_h = 0f;
        if (search != null)
        {
            search_h = search_height;
        }

        float list_h = total_h;
        float list_max = max_height - search_h;
        if (list_max < row_height)
        {
            list_max = row_height;
        }
        if (list_h > list_max)
        {
            list.is_scrollable = true;
            list_h = list_max;
        }

        layout.size = new Vector2(menu_width, search_h + list_h);
        layout.size_min = layout.size;

        if (search != null)
        {
            search.layout.size = new Vector2(menu_width, search_height);
            search.layout.size_min = search.layout.size;
            search.position = Vector2.Zero;
            search.transform.position = Vector2.Zero;
        }

        list.layout.size = new Vector2(menu_width, list_h);
        list.layout.size_min = list.layout.size;
        list.position = new Vector2(0, search_h);
        list.transform.position = new Vector2(0, search_h);
    }

    public void Place(Vector2 screen_pos)
    {
        float w = layout.size.X;
        float h = layout.size.Y;
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();
        if (screen_pos.X + w > sw)
        {
            screen_pos.X = sw - w;
        }
        if (screen_pos.Y + h > sh)
        {
            screen_pos.Y = sh - h;
        }
        if (screen_pos.X < 0)
        {
            screen_pos.X = 0;
        }
        if (screen_pos.Y < 0)
        {
            screen_pos.Y = 0;
        }
        transform.position = screen_pos;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.input_hog == null)
            {
                p.input_hog = this;
            }
        }
        if (on_escape != null && ImpPlayer.Key_IsPressed(EInputKey.Key_Escape))
        {
            on_escape();
        }
    }
}
