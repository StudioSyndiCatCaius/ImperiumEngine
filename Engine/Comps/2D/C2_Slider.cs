using System.Globalization;
using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_Slider : Imp2D
{
    public float value;
    public float min;
    public float max;
    public float step;

    public bool is_vertical;
    public bool is_spinner;
    public bool show_value_text = true;
    public int value_text_decimals = 2;
    public float drag_sensitivity = 0.05f;
    public Color accent_color = Color.Blank;

    public UI_Slider style = default;
    public Action<C2_Slider> on_changed;

    bool _hover;
    bool _drag;
    float _drag_raw;
    float _travel;
    C2_TextEdit _edit;

    const float ClickSlop = 3f;

    public bool HasRange => max > min;
    public bool IsBusy => _drag || IsTyping;
    public bool IsTyping => _edit != null && _edit.is_visible;

    public C2_Slider()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    public void Value_Set(float v, bool notify = true)
    {
        // Always writes through: the bound target can hold a different value than the
        // cached display (multi-select, external edits), so equality here proves nothing.
        value = Clamp(v);
        if (notify) on_changed?.Invoke(this);
    }

    public void Value_SetQuiet(float v) => value = Clamp(v);

    float Clamp(float v)
    {
        if (step > 0f) v = MathF.Round(v / step) * step;
        if (HasRange) v = Math.Clamp(v, min, max);
        return v;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (IsTyping)
        {
            if (_edit != null)
            {
                _edit.layout.orient_H = EUIViewportAlignment.Fill;
                _edit.layout.orient_V = EUIViewportAlignment.Fill;
            }
            return;
        }

        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];
        bool held = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);

        if (_hover && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left))
        {
            _drag = true;
            _drag_raw = value;
            _travel = 0f;
            player.input_hog = this;
        }

        if (_drag)
        {
            // A rebuilt row (or another widget grabbing input) leaves the drag orphaned.
            if (player.input_hog != null && player.input_hog != this)
            {
                _drag = false;
                return;
            }

            Vector2 md = Raylib.GetMouseDelta();
            _travel += MathF.Abs(md.X) + MathF.Abs(md.Y);
            bool spin = is_spinner || !HasRange;
            // Below the slop a spinner press is still a click-to-type, so it must not nudge the value.
            bool apply = !spin || _travel > ClickSlop;

            if (spin)
                _drag_raw += (is_vertical ? -md.Y : md.X) * drag_sensitivity;
            else
            {
                TDimensions2 dim = Dimensions_Get();
                float t = is_vertical
                    ? (dim.size.Y > 0 ? 1f - (player.cursor.position.Y - dim.position.Y) / dim.size.Y : 0)
                    : (dim.size.X > 0 ? (player.cursor.position.X - dim.position.X) / dim.size.X : 0);
                _drag_raw = min + Math.Clamp(t, 0f, 1f) * (max - min);
            }
            if (apply) Value_Set(_drag_raw);

            if (!held)
            {
                _drag = false;
                if (player.input_hog == this) player.input_hog = null;
                if (is_spinner && _travel <= ClickSlop) Type_Begin();
            }
        }
    }

    void Type_Begin()
    {
        if (_edit == null)
        {
            _edit = new C2_TextEdit();
            _edit.on_submit = Type_Commit;
            Child_Add(_edit);
        }
        _edit.is_visible = true;
        _edit.text = "";
        _edit.text_placeholder = Text_Value();
        _edit.is_focused = true;
        _edit.cursor = 0;
        if (ImpPlayer.players.Count > 0) ImpPlayer.players[0].input_hog = _edit;
    }

    void Type_Commit(string text)
    {
        if (_edit == null) return;
        _edit.is_visible = false;
        _edit.is_focused = false;
        if (ImpPlayer.players.Count > 0 && ImpPlayer.players[0].input_hog == _edit)
            ImpPlayer.players[0].input_hog = null;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            Value_Set(parsed);
    }

    string Text_Value()
    {
        return value_text_decimals <= 0
            ? MathF.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0." + new string('#', value_text_decimals), CultureInfo.InvariantCulture);
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (IsTyping) return;
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        style ??= UI_Slider.DEFAULT;
        style.style_background?.Draw(dim);

        if (!is_spinner && HasRange)
        {
            float t = (value - min) / (max - min);
            TDimensions2 fill = dim;
            if (is_vertical)
            {
                fill.size.Y = dim.size.Y * t;
                fill.position.Y = dim.position.Y + dim.size.Y - fill.size.Y;
            }
            else fill.size.X = dim.size.X * t;
            style.style_fill?.Draw(fill);
        }

        if (accent_color.A > 0)
            Raylib.DrawRectangleV(dim.position, new Vector2(2, dim.size.Y), accent_color);

        if (show_value_text && style.Text != null)
        {
            style.Text.Draw(Text_Value(), dim.position, dim.size, 0, ETextWrap.None,
                EUIPositionAlignment.Center, EUIPositionAlignment.Center);
        }
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin) _hover = true;
        else if (notify == ENotifyGeneric.End && !_drag) _hover = false;
    }
}
// ####################################################################################################################
// STYLE
// ####################################################################################################################

public struct TSliderConfig
{
    public float min;
    public float max;
    public float step;
    public bool is_vertical;
    public bool is_spinner;
    public bool show_value_text;
    public int value_text_decimals;
}

public class UI_Slider : ImpAsset
{
    // =====================================================================================================
    // Class
    // =====================================================================================================
    [ImpVar] public TImage slider_image;
    [ImpVar] public UiStyle_Box style_background = UiStyle_Box.STYLE_BKG_MID;
    [ImpVar] public UiStyle_Box style_fill = UiStyle_Box.STYLE_BTN_HOVER;
    [ImpVar] public UiStyle_Box style_slider_pressed = UiStyle_Box.STYLE_BTN_PRESS;
    [ImpVar] public UI_Text Text = UI_Text.LIGHT;
    
    // =====================================================================================================
    // Statics
    // =====================================================================================================

    public static void Draw(string label, out float value, TSliderConfig slider, UI_Slider style=default)
    {
        value = 0;
    }
    
    // ----------------------------------------------
    // Styles
    // ----------------------------------------------
    public static UI_Slider DEFAULT = new();

}
