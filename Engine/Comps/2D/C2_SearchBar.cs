using System.Numerics;
using ImperiumEngine.Enums;

namespace ImperiumEngine.Comps._2D;

public class C2_SearchBar : C2_Box
{
    public C2_TextEdit text_edit;
    public Action<string> on_search;
    public string placeholder = "Search Assets";

    public string Query => text_edit?.text ?? "";

    public C2_SearchBar()
    {
        style = UiStyle_Box.STYLE_BKG_DARK;
        cursor_filter = ECursorFilter.Pass;
        view_alighnment_H = EUIViewportAlignment.Fill;

        text_edit = new C2_TextEdit
        {
            text_placeholder = placeholder,
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            style = new UiStyle_TextEdit
            {
                style_background = new UiStyle_Box { tint = new Raylib_cs.Color(28, 28, 28, 255) },
                text_style = UiStyle_Text.LIGHT,
                placeholder_style = UiStyle_Text.MUTED,
            },
        };
        text_edit.on_text_changed = q => on_search?.Invoke(q ?? "");
        text_edit.on_text_cleared = _ => on_search?.Invoke("");
        text_edit.on_submit = q => on_search?.Invoke(q ?? "");
        Child_Add(text_edit);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (text_edit != null && text_edit.text_placeholder != placeholder)
            text_edit.text_placeholder = placeholder;
    }

    public void Query_Set(string value)
    {
        if (text_edit == null) return;
        text_edit.text = value ?? "";
        on_search?.Invoke(text_edit.text);
    }
}
