using System.Drawing;
using System.Numerics;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

// =====================================================================================================================
// Base
// =====================================================================================================================


internal abstract class O2D_PropWidget : ImpComponent2D
{
    protected readonly Func<object?>   _get;
    protected readonly Action<object?> _set;

    protected static readonly Color C_BgNormal = Color.FromArgb(36, 36, 42);
    protected static readonly Color C_BgHover  = Color.FromArgb(50, 50, 58);
    protected static readonly Color C_Text     = Color.FromArgb(215, 215, 225);
    protected static readonly Color C_TextDim  = Color.FromArgb(140, 140, 150);

    protected O2D_PropWidget(Func<object?> get, Action<object?> set)
    {
        _get = get; _set = set;
        mouse_filter        = EMouseFilter.Stop;
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
    }

    protected void DrawBg(ImpRender rhi) =>
        rhi.Draw2D_Rect(
            TVector2i.From(_rect.position + new Vector2(1, 1)),
            TVector2i.From(_rect.size    - new Vector2(2, 2)),
            hovered ? C_BgHover : C_BgNormal);

    protected void DrawText(ImpRender rhi, string text, Color? color = null)
    {
        var   m = rhi.Text_Measure(text, 12);
        float y = _rect.position.Y + (_rect.size.Y - m.y) * 0.5f;
        rhi.Draw2D_Text(new TVector2i((int)(_rect.position.X + 6), (int)y), text, 12, color ?? C_Text);
    }
}

// =====================================================================================================================
// Bool — click to toggle
// =====================================================================================================================

internal class O2DPropWidgetBool : O2D_PropWidget
{
    public O2DPropWidgetBool(Func<object?> g, Action<object?> s) : base(g, s) { }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        bool   v  = _get() is bool b && b;
        const float sz = 14f;
        DrawBg(rhi);
        var cp = new Vector2(_rect.position.X + 5f, _rect.position.Y + (_rect.size.Y - sz) * 0.5f);
        rhi.Draw2D_Rect(TVector2i.From(cp), new TVector2i((int)sz, (int)sz),
            v ? Color.FromArgb(80, 140, 255) : Color.FromArgb(55, 55, 62));
        if (v) DrawText(rhi, "✓", Color.White);
    }

    public override void OnClicked() => _set(!(_get() is bool b && b));
}

// =====================================================================================================================
// Numerics — display value (drag-to-edit can be added when input system supports it)
// =====================================================================================================================

internal class O2DPropWidgetInt : O2D_Spinner
{
    private readonly Func<object?> _get;

    public O2DPropWidgetInt(Func<object?> get, Action<object?> set)
    {
        _get           = get;
        value          = get() is int i ? i : 0;
        decimals       = 0;
        step           = 1.0;
        size_flags_h   = ESizeFlags.ExpandFill;
        OnValueChanged = v => set((int)Math.Round(v));
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (!focused && !_dragging) value = _get() is int i ? i : 0;
        base.OnDraw(rhi, dt);
    }
}

internal class O2DPropWidgetFloat : O2D_Spinner
{
    private readonly Func<object?> _get;

    public O2DPropWidgetFloat(Func<object?> get, Action<object?> set)
    {
        _get           = get;
        value          = get() is float f ? f : 0f;
        decimals       = 4;
        step           = 0.1;
        size_flags_h   = ESizeFlags.ExpandFill;
        OnValueChanged = v => set((float)v);
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (!focused && !_dragging) value = _get() is float f ? f : 0f;
        base.OnDraw(rhi, dt);
    }
}

internal class O2DPropWidgetDouble : O2D_Spinner
{
    private readonly Func<object?> _get;

    public O2DPropWidgetDouble(Func<object?> get, Action<object?> set)
    {
        _get           = get;
        value          = get() is double d ? d : 0.0;
        decimals       = 5;
        step           = 0.1;
        size_flags_h   = ESizeFlags.ExpandFill;
        OnValueChanged = v => set(v);
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (!focused && !_dragging) value = _get() is double d ? d : 0.0;
        base.OnDraw(rhi, dt);
    }
}

// =====================================================================================================================
// String / generic fallback
// =====================================================================================================================

internal class O2DPropWidgetStr : O2D_TextEdit
{
    private readonly Func<object?> _get;

    public O2DPropWidgetStr(Func<object?> get, Action<object?> set)
    {
        _get         = get;
        text         = get()?.ToString() ?? "";
        OnTextChanged = s => set(s);
        size_flags_h = ESizeFlags.ExpandFill;
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        // Keep display in sync when not being actively edited
        if (!focused) text = _get()?.ToString() ?? "";
        base.OnDraw(rhi, dt);
    }
}

internal class PropWidget_Label : ImpComponent2D
{
    private readonly Func<object?> _get;

    public PropWidget_Label(Func<object?> get)
    {
        _get                = get;
        mouse_filter        = EMouseFilter.Ignore;
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        string text = _get()?.ToString() ?? "(null)";
        var    m    = rhi.Text_Measure(text, 12);
        float  y    = _rect.position.Y + (_rect.size.Y - m.y) * 0.5f;
        rhi.Draw2D_Text(new TVector2i((int)(_rect.position.X + 6), (int)y), text, 12, Color.FromArgb(140, 140, 150));
    }
}

// =====================================================================================================================
// Color swatch
// =====================================================================================================================

internal class O2DPropWidgetColor : O2D_PropWidget
{
    public O2DPropWidgetColor(Func<object?> g, Action<object?> s) : base(g, s) { }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        DrawBg(rhi);
        if (_get() is not Color c) return;

        float swH = _rect.size.Y - 6f;
        float swW = swH * 2.5f;
        rhi.Draw2D_Rect(
            new TVector2i((int)(_rect.position.X + 4), (int)(_rect.position.Y + 3)),
            new TVector2i((int)swW, (int)swH), c);
        rhi.Draw2D_Text(
            new TVector2i((int)(_rect.position.X + swW + 10), (int)(_rect.position.Y + (_rect.size.Y - 12) * 0.5f)),
            $"#{c.R:X2}{c.G:X2}{c.B:X2}", 11, C_Text);
    }
}

// =====================================================================================================================
// Enum — click cycles through values, shows dropdown chevron
// =====================================================================================================================

internal class O2DPropWidgetEnum : O2D_PropWidget
{
    private readonly Array _values;

    public O2DPropWidgetEnum(Type enumType, Func<object?> g, Action<object?> s) : base(g, s)
        => _values = Enum.GetValues(enumType);

    public override void OnDraw(ImpRender rhi, double dt)
    {
        DrawBg(rhi);
        DrawText(rhi, _get()?.ToString() ?? "?");
        rhi.Draw2D_Text(
            new TVector2i((int)(_rect.position.X + _rect.size.X - 16), (int)(_rect.position.Y + (_rect.size.Y - 12) * 0.5f)),
            "▾", 11, C_TextDim);
    }

    public override void OnClicked()
    {
        var cur = _get();
        int idx = Array.IndexOf(_values, cur);
        _set(_values.GetValue((idx + 1) % _values.Length));
    }
}

// =====================================================================================================================
// Shared axis colours
// =====================================================================================================================

internal static class AxisStyle
{
    public static readonly Color X = Color.FromArgb(185, 55,  55);
    public static readonly Color Y = Color.FromArgb(60,  155, 55);
    public static readonly Color Z = Color.FromArgb(55,  100, 210);
    public static readonly Color W = Color.FromArgb(140, 80,  210);
}

// =====================================================================================================================
// Vector2 / Vector3 / Quaternion — axes side by side, each with a colour band
// =====================================================================================================================

internal class PropWidget_Vec2 : ImpComponent2D
{
    public PropWidget_Vec2(Func<object?> get, Action<object?> set)
    {
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
        layout_mode         = ELayoutMode.Horizontal;
        separation          = 2f;
        Add(0); Add(1);

        void Add(int axis)
        {
            var w = new O2DPropWidgetFloat(
                () => get() is Vector2 v ? (object)(axis == 0 ? v.X : v.Y) : 0f,
                sv  =>
                {
                    var c = get() is Vector2 cv ? cv : Vector2.Zero;
                    set(axis == 0 ? new Vector2((float)sv!, c.Y) : new Vector2(c.X, (float)sv!));
                });
            w.band_label   = axis == 0 ? "X" : "Y";
            w.band_color   = axis == 0 ? AxisStyle.X : AxisStyle.Y;
            w.size_flags_h = ESizeFlags.ExpandFill;
            Child_Add(w);
        }
    }
}

internal class PropWidget_Vec3 : ImpComponent2D
{
    public PropWidget_Vec3(Func<object?> get, Action<object?> set)
    {
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
        layout_mode         = ELayoutMode.Horizontal;
        separation          = 2f;
        Add(0); Add(1); Add(2);

        void Add(int axis)
        {
            var w = new O2DPropWidgetFloat(
                () => get() is Vector3 v ? (object)(axis == 0 ? v.X : axis == 1 ? v.Y : v.Z) : 0f,
                sv  =>
                {
                    var c = get() is Vector3 cv ? cv : Vector3.Zero;
                    set(axis switch
                    {
                        0 => new Vector3((float)sv!, c.Y, c.Z),
                        1 => new Vector3(c.X, (float)sv!, c.Z),
                        _ => new Vector3(c.X, c.Y, (float)sv!),
                    });
                });
            w.band_label   = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
            w.band_color   = axis == 0 ? AxisStyle.X : axis == 1 ? AxisStyle.Y : AxisStyle.Z;
            w.size_flags_h = ESizeFlags.ExpandFill;
            Child_Add(w);
        }
    }
}

// Quaternion shown as editable Euler angles (Roll/Pitch/Yaw in degrees), matching UE behaviour.
internal class PropWidget_Quat : ImpComponent2D
{
    private static readonly string[] Labels = { "R", "P", "Y" };
    private static readonly Color[]  Colors = { AxisStyle.X, AxisStyle.Y, AxisStyle.Z };

    public PropWidget_Quat(Func<object?> get, Action<object?> set)
    {
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
        layout_mode         = ELayoutMode.Horizontal;
        separation          = 2f;
        Add(0); Add(1); Add(2);

        void Add(int axis)
        {
            var w = new O2DPropWidgetFloat(
                () =>
                {
                    var q = get() is System.Numerics.Quaternion qv ? qv : System.Numerics.Quaternion.Identity;
                    var e = Quat_ToEuler(q);
                    return (object)(axis == 0 ? e.X : axis == 1 ? e.Y : e.Z);
                },
                sv =>
                {
                    var q = get() is System.Numerics.Quaternion qv ? qv : System.Numerics.Quaternion.Identity;
                    var e = Quat_ToEuler(q);
                    var ne = axis switch
                    {
                        0 => new Vector3((float)sv!, e.Y, e.Z),
                        1 => new Vector3(e.X, (float)sv!, e.Z),
                        _ => new Vector3(e.X, e.Y, (float)sv!),
                    };
                    set(Quat_FromEuler(ne));
                });
            w.band_label   = Labels[axis];
            w.band_color   = Colors[axis];
            w.size_flags_h = ESizeFlags.ExpandFill;
            w.suffix       = "°";
            w.step         = 1.0;
            w.decimals     = 2;
            Child_Add(w);
        }
    }

    // Quaternion → Euler degrees (Roll=X, Pitch=Y, Yaw=Z)
    private static Vector3 Quat_ToEuler(System.Numerics.Quaternion q)
    {
        float roll  = MathF.Atan2(2f * (q.W * q.X + q.Y * q.Z),
                                  1f - 2f * (q.X * q.X + q.Y * q.Y));
        float sinp  = 2f * (q.W * q.Y - q.Z * q.X);
        float pitch = MathF.Abs(sinp) >= 1f
                    ? MathF.CopySign(MathF.PI * 0.5f, sinp)
                    : MathF.Asin(sinp);
        float yaw   = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y),
                                  1f - 2f * (q.Y * q.Y + q.Z * q.Z));

        const float Rad2Deg = 180f / MathF.PI;
        return new Vector3(roll * Rad2Deg, pitch * Rad2Deg, yaw * Rad2Deg);
    }

    // Euler degrees → Quaternion
    private static System.Numerics.Quaternion Quat_FromEuler(Vector3 deg)
    {
        const float Deg2Rad = MathF.PI / 180f;
        return System.Numerics.Quaternion.CreateFromYawPitchRoll(
            deg.Z * Deg2Rad,   // yaw
            deg.Y * Deg2Rad,   // pitch
            deg.X * Deg2Rad);  // roll
    }
}
