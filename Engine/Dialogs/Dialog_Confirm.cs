using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_Confirm : ImpDialog
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

    public static void Run(string message, Action on_yes, Action on_no = null, string text_yes = "Yes", string text_no = "No")
    {
        Dialog_Confirm dlg = new()
        {
            message = message,
            on_yes = on_yes,
            on_no = on_no,
            text_yes = text_yes,
            text_no = text_no,
        };
        dlg.Show();
    }

    public Dialog_Confirm()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
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
        on_dismiss = No;
        base.Show();
        Overlay?.Child_Add(box);
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
