using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_Confirm : C1_Dialog
{
    public string message;
    public string text_yes = "Yes";
    public string text_no = "No";
    public Action on_yes;
    public Action on_no;

    C2_Box box = new();
    C2_Text text = new();
    C2_Button btn_yes = new();
    C2_Button btn_no = new();
    bool _ui;

    public static void Run(string _message, Action _on_yes, Action _on_no = null)
    {
        Dialog_Confirm dlg = new()
        {
            message = _message,
            on_yes = _on_yes,
            on_no = _on_no,
        };
        dlg.Show();
    }

    public Dialog_Confirm()
    {
        box.style = new UiStyle_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(400, 140),
            size_min = new Vector2(320, 120),
        };

        text.style = UI_Text.LIGHT;
        text.wrap = ETextWrap.Word;
        text.text_alignment_v = EUIPositionAlignment.Center;
        text.layout = new TLayout2
        {
            size = new Vector2(0, 60),
            size_min = new Vector2(0, 40),
            orient_H = EUIViewportAlignment.Fill,
        };

        btn_yes.on_click = Yes;
        btn_no.on_click = No;
    }

    public override void Show()
    {
        EnsureUi();
        text.text = message ?? "";
        btn_yes.text = string.IsNullOrEmpty(text_yes) ? "Yes" : text_yes;
        btn_no.text = string.IsNullOrEmpty(text_no) ? "No" : text_no;
        base.Show();
        if (Shade != null)
        {
            Shade.on_click = No;
        }
        if (Overlay is C2_DialogHost host)
        {
            host.on_escape = No;
        }
        Overlay?.Child_Add(box);
    }

    public override void Close()
    {
        Hog_Release();
        box?.Detach();
        base.Close();
    }

    void EnsureUi()
    {
        if (_ui)
        {
            return;
        }
        _ui = true;

        C2_List col = new()
        {
            orentation = EUIOrentation.V,
            is_scrollable = false,
            spacing = 8,
            layout = TLayout2.FULL,
        };

        btn_no.layout = new TLayout2
        {
            size = new Vector2(90, 28),
            size_min = new Vector2(80, 28),
        };
        btn_yes.layout = new TLayout2
        {
            size = new Vector2(90, 28),
            size_min = new Vector2(80, 28),
        };

        C2_List btns = new()
        {
            orentation = EUIOrentation.H,
            spacing = 8,
            layout = new TLayout2
            {
                size = new Vector2(0, 30),
                size_min = new Vector2(0, 30),
                orient_H = EUIViewportAlignment.Fill,
            },
        };
        btns.Child_Add(btn_no);
        btns.Child_Add(btn_yes);

        col.Child_Add(text);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void Yes()
    {
        on_yes?.Invoke();
        if (is_open)
        {
            Close();
        }
    }

    void No()
    {
        on_no?.Invoke();
        if (is_open)
        {
            Close();
        }
    }
}
