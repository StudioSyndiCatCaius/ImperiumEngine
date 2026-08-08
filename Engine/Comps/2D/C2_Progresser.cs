using System.Globalization;
using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public enum EProgresserTextStyle
{
    None, Percentage, Value,
}

// Combo of a slider and a progress bar.
//
// With a real range (max > min) it fills proportionally and the mouse sets the value by
// absolute position, like a slider. With no range it degrades to a drag-number: no fill,
// and dragging nudges the value by mouse movement. That second mode is what unbounded
// numeric fields in the inspector use.
public class C2_Progresser : ImpComp2D
{
    [ImpVar] public UIStyle_Progresser? style;
    [ImpVar] public float value;
    [ImpVar] public float min_value;
    [ImpVar] public float max_value;
    [ImpVar] public float step_amount;
    [ImpVar] public Color fill_color = Color.Blank;
    [ImpVar] public EProgresserTextStyle text_style = EProgresserTextStyle.Value;
    [ImpVar] public bool mouse_can_edit; //allow user to edit value with mouse click & drag, like a slider

    //value units per pixel of drag in the unbounded mode
    [ImpVar] public float drag_sensitivity = 0.1f;
    //digits shown by the Value text style; 0 renders as a whole number
    [ImpVar] public int decimals = 2;
    //inset for the value text; negative takes the theme's padding, which is too generous
    //once several of these share a row
    [ImpVar] public float text_inset = -1f;

    //thin colour tab down the leading edge; a vector row tags each axis with one
    [ImpVar] public Color accent_color = Color.Blank;
    [ImpVar] public float accent_width = 2f;
    
    [ImpVar] public bool can_type_edit; //allow user to edit value by clicking and typing on box
    C2_TextEdit? value_text_edit; //the text edit component for the value field.

    //pixels of travel that still count as a click rather than a drag
    const float CLICK_SLOP = 3f;

    public Action<C2_Progresser>? on_value_changed;

    float drag_raw; //unquantised value while dragging, so sub-step movement isn't lost
    float drag_travel; //how far the pointer moved while held, to tell a click from a drag
    bool dragging;

    public bool IsTyping => value_text_edit != null && value_text_edit.is_visible;

    UIStyle_Text StyleText => style?.text_style ?? Theme_Get().style_text;

    public bool HasRange => max_value > min_value;
    public float Fraction => HasRange ? Math.Clamp((value - min_value) / (max_value - min_value), 0f, 1f) : 0f;

    public override Vector2 Size_GetContentMin() => new Vector2(0, Theme_Get().item_height);

    // ---------------------------------------------------
    // value
    // ---------------------------------------------------

    public void Value_Set(float v, bool notify = true)
    {
        v = Value_Clamp(v);
        if (v == value) return;

        value = v;
        if (notify) on_value_changed?.Invoke(this);
    }

    public void Value_SetQuiet(float v)
    {
        value = Value_Clamp(v);
    }

    float Value_Clamp(float v)
    {
        if (step_amount > 0f) v = MathF.Round(v / step_amount) * step_amount;
        if (HasRange) v = Math.Clamp(v, min_value, max_value);
        return v;
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    // Runs from the draw pass: ImpUI.IsPressed is exactly "button went down on me and is
    // still held", which is the drag state this needs.
    void Input_Update()
    {
        if (!mouse_can_edit || IsTyping) return;

        bool held = ImpUI.IsPressed(this);

        if (held && !dragging)
        {
            dragging = true;
            drag_raw = value;
            drag_travel = 0f;
        }
        else if (!held)
        {
            dragging = false;
            return;
        }

        drag_travel += MathF.Abs(ImpUI.mouse_delta.X) + MathF.Abs(ImpUI.mouse_delta.Y);

        if (HasRange)
        {
            float t = rect.Width > 0f ? (ImpUI.mouse_pos.X - rect.X) / rect.Width : 0f;
            drag_raw = min_value + Math.Clamp(t, 0f, 1f) * (max_value - min_value);
        }
        else
        {
            drag_raw += ImpUI.mouse_delta.X * drag_sensitivity;
        }

        Value_Set(drag_raw);
    }

    // ---------------------------------------------------
    // typing
    // ---------------------------------------------------

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (ev != ECursorEvent.Clicked || !can_type_edit || IsTyping) return;

        // A drag-number can't tell a click from the start of a drag until the button comes
        // back up, so the decision waits for the release and asks how far the pointer went.
        if (mouse_can_edit && drag_travel > CLICK_SLOP) return;

        // A slider with a range already jumped to wherever it was clicked; opening a box
        // over the top would fight the value it just set.
        if (HasRange && mouse_can_edit) return;

        Type_Begin();
    }

    void Type_Begin()
    {
        if (value_text_edit == null)
        {
            value_text_edit = new C2_TextEdit
            {
                name = "Value",
                anchor_preset = EUIAnchorPreset.Full,
            };

            value_text_edit.on_submit = Type_Commit;
            Child_Add(value_text_edit);
        }

        // Opens empty, showing the current value as the hint, because there is no text
        // selection to replace: seeding the box with "0.000" and a caret at the front turns
        // typing 12.5 into 12.50.000. Typing nothing and leaving keeps the value as it was.
        value_text_edit.is_visible = true;
        value_text_edit.Text_SetQuiet("");
        value_text_edit.placeholder_text = Text_Value();

        // Layout ran before input this frame, so the field has no rect yet and would draw
        // as a sliver at the origin. Give it this comp's rect now; the next layout agrees.
        value_text_edit.OnLayout_Exact(rect);

        ImpUI.focused = value_text_edit;
    }

    // Fires on enter and on focus loss alike, so clicking away commits what was typed.
    void Type_Commit(string text)
    {
        if (value_text_edit == null) return;

        value_text_edit.is_visible = false;
        Rect_Clear(value_text_edit);

        if (ImpUI.focused == value_text_edit) ImpUI.focused = null;

        //unparseable text leaves the value alone rather than snapping it to zero
        if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float parsed))
        {
            Value_Set(parsed);
        }
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        Input_Update();

        var bg = style?.box_background?.color
                 ?? (ImpUI.IsPressed(this) ? th.col_pressed
                     : ImpUI.IsHovered(this) && mouse_can_edit ? th.col_hover
                     : th.col_panel_alt);

        ImpUI.Rect(rect, bg);

        if (HasRange)
        {
            var fill = fill_color.A > 0 ? fill_color : style?.box_fill?.color ?? th.col_accent;
            ImpUI.Rect(new Rectangle(rect.X, rect.Y, rect.Width * Fraction, rect.Height), fill);

            if (style?.show_slider_bar == true) Grip_Draw(th);
        }

        if (accent_color.A > 0 && accent_width > 0f)
        {
            ImpUI.Rect(new Rectangle(rect.X, rect.Y, accent_width, rect.Height), accent_color);
        }

        string label = Text_Get();
        if (label.Length == 0) return;

        // A value can always be longer than the box holding it - three coordinates sharing
        // one inspector row leaves each barely wide enough for "0.000", and "150.000" ran
        // straight over the next field's tag. Clip so a number can only spill inside its box.
        ImpUI.Clip_Push(rect);

        // centred over a fill bar reads as a progress label; a bare drag-field reads
        // better as a left-aligned value, like every other field in a property list
        if (HasRange)
        {
            ImpUI.TextInRect(label, rect, StyleText, 0.5f);
        }
        else
        {
            float pad = text_inset >= 0f ? text_inset : th.padding;
            float lead = MathF.Max(pad, accent_color.A > 0 ? accent_width + pad : pad);

            var tr = new Rectangle(rect.X + lead, rect.Y,
                                   MathF.Max(0, rect.Width - lead - pad), rect.Height);
            ImpUI.TextInRect(label, tr, StyleText, 0f);
        }

        ImpUI.Clip_Pop();
    }

    void Grip_Draw(ImpUITheme th)
    {
        var s = style!.slider_bar_size;
        float w = s.X > 0 ? s.X : 4f;
        float h = s.Y > 0 ? s.Y : rect.Height;

        ImpUI.Rect(new Rectangle(
            rect.X + (rect.Width - w) * Fraction,
            rect.Y + (rect.Height - h) * 0.5f,
            w, h), th.col_text);
    }

    string Text_Get()
    {
        //the box is drawn by the text field while typing, and would otherwise show through it
        if (IsTyping) return "";

        return text_style switch
        {
            EProgresserTextStyle.Percentage => $"{Fraction * 100f:0}%",
            EProgresserTextStyle.Value => Text_Value(),
            _ => "",
        };
    }

    // Invariant, so what is displayed is also what Type_Commit can parse back.
    string Text_Value() => value.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);
}

public class UIStyle_Progresser : ImpAsset
{
    [ImpVar] public UIStyle_Rect? box_background;
    [ImpVar] public UIStyle_Rect? box_fill;
    [ImpVar] public UIStyle_Text? text_style;
    [ImpVar] public bool show_slider_bar;
    [ImpVar] public UIStyle_Text? slider_bar_style;
    [ImpVar] public Vector2 slider_bar_size;
}
