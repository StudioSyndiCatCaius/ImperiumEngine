using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_Alert : ImpDialog
{
    public string message;
    public string text_ok = "OK";
    public Action on_ok;

    C2_Box box = new();
    C2_Text text = new();
    C2_Button btn_ok = new();
    bool _ui;

    public static void Run(string message, Action on_ok = null, string text_ok = "OK")
    {
        Dialog_Alert dlg = new()
        {
            message = message,
            on_ok = on_ok,
            text_ok = text_ok,
        };
        dlg.Show();
    }

    public Dialog_Alert()
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

        btn_ok.on_click = Ok;
    }

    public override void Show()
    {
        EnsureUi();
        text.text = message ?? "";
        btn_ok.text = string.IsNullOrEmpty(text_ok) ? "OK" : text_ok;
        on_dismiss = Ok;
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

        btn_ok.layout = new TLayout2
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
        btns.Child_Add(btn_ok);

        col.Child_Add(text);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void Ok()
    {
        on_ok?.Invoke();
        if (is_open)
        {
            Close();
        }
    }
}
