using System.Drawing;
using System.Globalization;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;
using Silk.NET.Input;

namespace ImperiumEngine.Objects._2D;

// Numeric input: drag horizontally to change value, or click to type a number.
public class O2D_Spinner : ImpComponent2D
{
    public A_UiStyle_Spinner? style;

    public double  value;
    public double  step     = 1.0;      // magnitude of one drag-pixel change
    public int     decimals = 3;        // displayed decimal places (0 = integer display)
    public string? suffix;              // optional unit label, e.g. "°" or " px"

    public Color?  band_color;          // coloured strip on the left edge (axis colour)
    public string? band_label;          // short label inside the band, e.g. "X", "Y", "Z"
    private const float BandW = 16f;

    public Action<double>? OnValueChanged;

    // ---- edit (type-in) state ----
    private bool   _editing;
    private string _editBuf    = "";
    private int    _editCursor;
    private double _blinkTimer;
    private bool   _cursorVisible = true;

    // ---- drag state ----
    protected bool   _dragging;
    private float  _dragOriginX;
    private double _dragOriginVal;
    private const float  DragThreshold   = 5f;
    private const double DragSensitivity = 0.1; // value units per pixel

    // ---- default colors (overridden by style) ----
    private static readonly Color C_Bg        = Color.FromArgb(20, 20, 24);
    private static readonly Color C_BgHover   = Color.FromArgb(30, 30, 36);
    private static readonly Color C_BgActive  = Color.FromArgb(16, 16, 20);
    private static readonly Color C_Text      = Color.FromArgb(210, 210, 222);
    private static readonly Color C_Border    = Color.FromArgb(75, 135, 245);
    private static readonly Color C_Cursor    = Color.FromArgb(160, 200, 255);
    private static readonly Color C_DragFill  = Color.FromArgb(40, 80, 140, 200);

    public O2D_Spinner()
    {
        mouse_filter        = EMouseFilter.Stop;
        custom_minimum_size = new Vector2(0, 24);
    }

    // -----------------------------------------------------------------------------------------

    public override void OnFocus()
    {
        _blinkTimer    = 0;
        _cursorVisible = true;
    }

    public override void OnUnfocus()
    {
        if (_editing) Edit_Commit();
        _editing   = false;
        _dragging  = false;
    }

    public override void OnMousePressed(int button)
    {
        if (button != 0) return;
        _dragging      = false;
        _dragOriginX   = ImpApp.Active?.players[0].MousePos.X ?? 0;
        _dragOriginVal = value;
    }

    public override void OnMouseReleased(int button)
    {
        if (button != 0) return;
        if (!_dragging && !_editing) Edit_Enter();
        _dragging = false;
    }

    protected  override void OnUpdate(double dt)
    {
        // Drag detection — runs while mouse button is held (pressed flag set by player)
        if (pressed && !_editing)
        {
            float mx    = ImpApp.Active?.players[0].MousePos.X ?? _dragOriginX;
            float delta = mx - _dragOriginX;

            if (!_dragging && MathF.Abs(delta) > DragThreshold)
                _dragging = true;

            if (_dragging)
            {
                value = _dragOriginVal + delta * DragSensitivity * step;
                OnValueChanged?.Invoke(value);
            }
        }

        // Edit cursor blink
        if (_editing && focused)
        {
            _blinkTimer += dt;
            if (_blinkTimer >= 0.53) { _blinkTimer -= 0.53; _cursorVisible = !_cursorVisible; }
        }
    }

    public override void OnKeyChar(char c)
    {
        if (!_editing || char.IsControl(c)) return;
        if (!char.IsDigit(c) && c != '-' && c != '.' && c != ',') return;
        if (c == ',') c = '.';
        _editBuf    = _editBuf[.._editCursor] + c + _editBuf[_editCursor..];
        _editCursor++;
        Blink_Reset();
    }

    public override void OnKeyDown(Key key)
    {
        if (!_editing) return;
        switch (key)
        {
            case Key.Backspace: if (_editCursor > 0) { _editBuf = _editBuf[..(_editCursor-1)] + _editBuf[_editCursor..]; _editCursor--; } break;
            case Key.Delete:    if (_editCursor < _editBuf.Length) _editBuf = _editBuf[.._editCursor] + _editBuf[(_editCursor+1)..]; break;
            case Key.Left:      if (_editCursor > 0) _editCursor--;              break;
            case Key.Right:     if (_editCursor < _editBuf.Length) _editCursor++; break;
            case Key.Home:      _editCursor = 0;               break;
            case Key.End:       _editCursor = _editBuf.Length; break;
            case Key.Enter:     Edit_Commit(); ImpApp.Active?.players[0].Focus_Clear(); break;
            case Key.Escape:    _editing = false; ImpApp.Active?.players[0].Focus_Clear(); break;
        }
        Blink_Reset();
    }

    protected  override void OnDraw(ImpRender rhi, double dt)
    {
        var s    = style;
        var bg   = _editing || _dragging ? (s?.color_bg_active  ?? C_BgActive)
                 : hovered               ? (s?.color_bg_hover   ?? C_BgHover)
                                         : (s?.color_bg         ?? C_Bg);

        rhi.Draw2D_Rect(TVector2i.From(_rect.position + new Vector2(1, 1)),
                        TVector2i.From(_rect.size    - new Vector2(2, 2)), bg);

        // Drag fill: blue bar from drag origin to current mouse X
        if (_dragging)
        {
            float mx     = ImpApp.Active?.players[0].MousePos.X ?? _rect.position.X;
            float left   = MathF.Min(mx, _dragOriginX);
            float right  = MathF.Max(mx, _dragOriginX);
            float clampL = MathF.Max(left,  _rect.position.X + 1);
            float clampR = MathF.Min(right, _rect.position.X + _rect.size.X - 1);
            if (clampR > clampL)
                rhi.Draw2D_Rect(
                    new TVector2i((int)clampL, (int)(_rect.position.Y + 1)),
                    new TVector2i((int)(clampR - clampL), (int)(_rect.size.Y - 2)),
                    s?.color_drag_fill ?? C_DragFill);
        }

        // Focus border (bottom line)
        if (focused)
            rhi.Draw2D_Rect(
                new TVector2i((int)(_rect.position.X + 1), (int)(_rect.position.Y + _rect.size.Y - 2)),
                new TVector2i((int)(_rect.size.X - 2), 2),
                s?.color_border ?? C_Border);

        float tx = _rect.position.X + 6;
        float ty = _rect.position.Y + (_rect.size.Y - 14) * 0.5f;

        if (band_color.HasValue)
        {
            rhi.Draw2D_Rect(
                new TVector2i((int)(_rect.position.X + 1), (int)(_rect.position.Y + 1)),
                new TVector2i((int)BandW, (int)(_rect.size.Y - 2)),
                band_color.Value);
            if (!string.IsNullOrEmpty(band_label))
            {
                var bm = rhi.Text_Measure(band_label, 10);
                rhi.Draw2D_Text(
                    new TVector2i(
                        (int)(_rect.position.X + 1 + (BandW - bm.x) * 0.5f),
                        (int)(_rect.position.Y + (_rect.size.Y - bm.y) * 0.5f)),
                    band_label, 10, Color.White);
            }
            tx = _rect.position.X + BandW + 5;
        }

        var textColor = s?.color_text ?? C_Text;

        if (_editing)
        {
            rhi.Draw2D_Text(new TVector2i((int)tx, (int)ty), _editBuf, 13, textColor);
            if (_cursorVisible && focused)
            {
                var m = rhi.Text_Measure(_editCursor > 0 ? _editBuf[.._editCursor] : "", 13);
                rhi.Draw2D_Rect(new TVector2i((int)(tx + m.x), (int)ty), new TVector2i(1, 14),
                    s?.color_cursor ?? C_Cursor);
            }
        }
        else
        {
            string text = Value_Format() + (suffix ?? "");
            rhi.Draw2D_Text(new TVector2i((int)tx, (int)ty), text, 13, textColor);
        }
    }

    // -----------------------------------------------------------------------------------------

    protected virtual string Value_Format()
    {
        if (decimals == 0) return ((long)Math.Round(value)).ToString();
        string s = value.ToString("G" + (decimals + 2), CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        return s;
    }

    private void Edit_Enter()
    {
        _editing    = true;
        _editBuf    = Value_Format();
        _editCursor = _editBuf.Length;
        Blink_Reset();
    }

    private void Edit_Commit()
    {
        _editing = false;
        if (double.TryParse(_editBuf, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            value = parsed;
            OnValueChanged?.Invoke(value);
        }
    }

    private void Blink_Reset() { _blinkTimer = 0; _cursorVisible = true; }
}

public class A_UiStyle_Spinner : ImpAsset
{
    public Color color_bg         = Color.FromArgb(20, 20, 24);
    public Color color_bg_hover   = Color.FromArgb(30, 30, 36);
    public Color color_bg_active  = Color.FromArgb(16, 16, 20);
    public Color color_text       = Color.FromArgb(210, 210, 222);
    public Color color_drag_fill  = Color.FromArgb(40, 80, 140, 200);
    public Color color_border     = Color.FromArgb(75, 135, 245);
    public Color color_cursor     = Color.FromArgb(160, 200, 255);
}
