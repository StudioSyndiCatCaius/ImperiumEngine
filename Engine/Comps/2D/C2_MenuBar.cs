using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._2D;

public struct TMenuBarOption
{
    public string text;
    public List<TMenuBarSubption> suboptions = new();
    public Action on_press;

    public TMenuBarOption()
    {
        text = "";
    }
}

public struct TMenuBarSubption
{
    public string text;
    public bool is_separator;
    public bool is_disabled;
    public List<TMenuBarSubption> suboptions = new();

    public EInputKey hotkey_key;
    public bool hotkey_require_ctrl = true;
    public bool hotkey_require_shift = false;
    public bool hotkey_require_alt = false;

    public Action on_press;

    public TMenuBarSubption()
    {
        text = "";
        is_separator = false;
        is_disabled = false;
    }

    public bool Hotkey_CanPress()
    {
        // Modifiers have to match exactly, or Ctrl+Shift+Z would also fire the plain Ctrl+Z
        // entry sitting above it. Held rather than down so a modifier pressed on the same
        // frame as the key still counts.
        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        bool shift = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift);
        bool alt = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftAlt) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightAlt);
        return ctrl == hotkey_require_ctrl && shift == hotkey_require_shift && alt == hotkey_require_alt;
    }
}

[ImpClass(Hidden = true)]
public class C2_MenuBar : Imp2D
{
    public List<TMenuBarOption> options = new();

    public C2_List list_options = new()
    {
        orentation = EUIOrentation.H,
        is_scrollable = false,
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };

    public C2_MenuBar_List open_list;
    public int open_index = -1;

    public UiStyle_Box style_background = UiStyle_Box.STYLE_BKG_DARK;
    public UiStyle_Box style_options_idle = UiStyle_Box.STYLE_BTN_IDLE;
    public UiStyle_Box style_options_hovered = UiStyle_Box.STYLE_BTN_HOVER;
    public UiStyle_Box style_options_pressed = UiStyle_Box.STYLE_BTN_PRESS;

    public float option_width = 72;
    public float dropdown_width = 180;
    public float row_height = 28;

    int _built_count = -1;

    public C2_MenuBar()
    {
        cursor_filter = ECursorFilter.Pass;
        Child_Add(list_options);

        list_options.on_option_select = OnTopSelect;
        list_options.on_option_hover = OnTopHover;
    }

    public bool IsMenuOpen => open_list != null;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);

        if (_built_count != options.Count)
            Rebuild();

        if (IsMenuOpen && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left))
        {
            ImpComp target = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].target_cursor : null;
            // open_list / cascades may live on the scene root (above other UI)
            if (!IsMenuUi(target))
                CloseAll();
        }

        for (int i = 0; i < options.Count; i++)
            Hotkey_Walk(options[i].suboptions);
    }

    public void Rebuild()
    {
        list_options.Child_RemoveAll();
        for (int i = 0; i < options.Count; i++)
        {
            float h = layout.size.Y > 0 ? layout.size.Y : row_height;
            C2_Button btn = new()
            {
                text = options[i].text ?? "",
                layout = new TLayout2
                {
                    size = new Vector2(option_width, h),
                    size_min = new Vector2(option_width, h),
                },
                style = new UI_Button
                {
                    style_unhovered = style_options_idle,
                    style_hovered = style_options_hovered,
                    style_pressed = style_options_pressed,
                },
                text_style = UI_Text.DEFAULT,
            };
            list_options.Child_Add(btn);
        }
        _built_count = options.Count;
    }

    public void CloseAll()
    {
        if (open_list != null)
        {
            open_list.CloseCascade();
            open_list.Destroy();
            open_list = null;
        }
        open_index = -1;
    }

    void OnTopSelect(Imp2D c, int i)
    {
        if (i < 0 || i >= options.Count) return;
        TMenuBarOption opt = options[i];

        if (opt.suboptions == null || opt.suboptions.Count == 0)
        {
            opt.on_press?.Invoke();
            CloseAll();
            return;
        }

        if (open_index == i)
        {
            CloseAll();
            return;
        }

        OpenRoot(i);
    }

    void OnTopHover(Imp2D c, int i)
    {
        if (!IsMenuOpen) return;
        if (i < 0 || i >= options.Count) return;
        if (open_index == i) return;
        if (options[i].suboptions == null || options[i].suboptions.Count == 0) return;
        OpenRoot(i);
    }

    void OpenRoot(int i)
    {
        CloseAll();
        open_index = i;
        open_list = new C2_MenuBar_List(this, options[i].suboptions)
        {
            layout = new TLayout2
                {
                    size = new Vector2(dropdown_width, 0),
                },
        };
        // parent to scene root so dropdown draws above TabBox / other siblings
        ImpComp host = PopupHost() ?? this;
        host.Child_Add(open_list);
        PlaceUnderTop(i, open_list);
        open_list.RebuildRows();
    }

    void PlaceUnderTop(int i, C2_MenuBar_List list)
    {
        if (i < 0 || i >= list_options.children.Count) return;
        if (list_options.children[i] is not Imp2D btn) return;

        // screen-space when parented to non-2D root; local if fallback parent is the bar
        TDimensions2 b = btn.Dimensions_Get();
        if (list.parent is Imp2D p2)
        {
            TDimensions2 host = p2.Dimensions_Get();
            list.transform.position = new Vector2(
                b.position.X - host.position.X,
                b.position.Y - host.position.Y + b.size.Y);
        }
        else
            list.transform.position = new Vector2(b.position.X, b.position.Y + b.size.Y);
    }

    /// <summary>Topmost host so menus render above the rest of the UI tree.</summary>
    public static ImpComp? PopupHost()
    {
        return ImpScene.current?.root;
    }

    void Hotkey_Walk(List<TMenuBarSubption> items)
    {
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            TMenuBarSubption s = items[i];
            if (!s.is_separator && !s.is_disabled
                && s.hotkey_key != EInputKey.None
                && s.Hotkey_CanPress()
                && ImpPlayer.Key_IsPressed(s.hotkey_key))
            {
                s.on_press?.Invoke();
                CloseAll();
                return;
            }
            if (s.suboptions != null && s.suboptions.Count > 0)
                Hotkey_Walk(s.suboptions);
        }
    }

    public static bool IsUnder(ImpComp root, ImpComp target)
    {
        while (target != null)
        {
            if (target == root) return true;
            target = target.parent;
        }
        return false;
    }

    public static bool IsMenuUi(ImpComp target)
    {
        while (target != null)
        {
            if (target is C2_MenuBar or C2_MenuBar_List) return true;
            target = target.parent;
        }
        return false;
    }
}

public class C2_MenuBar_List : C2_List
{
    public C2_MenuBar bar;
    public List<TMenuBarSubption> items = new();
    public C2_MenuBar_List cascade;
    public int cascade_index = -1;

    public C2_MenuBar_List(C2_MenuBar bar, List<TMenuBarSubption> items)
    {
        this.bar = bar;
        this.items = items ?? new List<TMenuBarSubption>();
        orentation = EUIOrentation.V;
        is_scrollable = false;
        spacing = 0;
        cursor_filter = ECursorFilter.Pass;

        on_option_select = OnRowSelect;
        on_option_hover = OnRowHover;
        on_option_unhover = OnRowUnhover;
    }

    public void RebuildRows()
    {
        Child_RemoveAll();
        cascade = null;
        cascade_index = -1;

        float w = layout.size.X > 0 ? layout.size.X : bar.dropdown_width;
        float h = bar.row_height;
        float total_h = 0;

        for (int i = 0; i < items.Count; i++)
        {
            TMenuBarSubption s = items[i];
            if (s.is_separator)
            {
                Imp2D sep = new()
                {
                    layout = new TLayout2
                    {
                        size = new Vector2(w, 6),
                    },
                    cursor_filter = ECursorFilter.Ignore,
                };
                Child_Add(sep);
                total_h += 6;
                continue;
            }

            C2_Button btn = new()
            {
                text = s.text ?? "",
                layout = new TLayout2
                {
                    size = new Vector2(w, h),
                },
                is_disabled = s.is_disabled,
                style = new UI_Button
                {
                    style_unhovered = bar.style_options_idle,
                    style_hovered = bar.style_options_hovered,
                    style_pressed = bar.style_options_pressed,
                },
            };
            Child_Add(btn);
            total_h += h;
        }

        layout.size = new Vector2(w, total_h);
    }

    void OnRowSelect(Imp2D c, int i)
    {
        if (i < 0 || i >= items.Count) return;
        TMenuBarSubption s = items[i];
        if (s.is_separator || s.is_disabled) return;

        if (s.suboptions != null && s.suboptions.Count > 0)
        {
            OpenCascade(i);
            return;
        }

        s.on_press?.Invoke();
        bar.CloseAll();
    }

    void OnRowHover(Imp2D c, int i)
    {
        if (i < 0 || i >= items.Count) return;
        TMenuBarSubption s = items[i];
        if (s.is_separator || s.is_disabled)
        {
            CloseCascade();
            return;
        }

        if (s.suboptions != null && s.suboptions.Count > 0)
            OpenCascade(i);
        else
            CloseCascade();
    }

    void OnRowUnhover(Imp2D c, int i)
    {
        // cascade stays until hover moves to another row / outside
    }

    void OpenCascade(int i)
    {
        if (cascade_index == i && cascade != null) return;
        CloseCascade();
        cascade_index = i;
        cascade = new C2_MenuBar_List(bar, items[i].suboptions)
        {
            layout = new TLayout2
                {
                    size = new Vector2(bar.dropdown_width, 0),
                },
        };
        // same popup host as root dropdown (not this list — layout would stack it as a row)
        ImpComp host = C2_MenuBar.PopupHost() ?? bar;
        host.Child_Add(cascade);
        cascade.RebuildRows();

        if (i < children.Count && children[i] is Imp2D row)
        {
            TDimensions2 self = Dimensions_Get();
            TDimensions2 r = row.Dimensions_Get();
            cascade.transform.position = new Vector2(self.position.X + layout.size.X, r.position.Y);
        }
    }

    public void CloseCascade()
    {
        if (cascade != null)
        {
            cascade.CloseCascade();
            cascade.Destroy();
            cascade = null;
        }
        cascade_index = -1;
    }
}


public class UI_MenuBar : ImpAsset
{
    public static UI_MenuBar DEFAULT = new();
}
