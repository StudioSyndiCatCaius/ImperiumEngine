using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using ImperiumCore;
using ImperiumCore.Assets;
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

    protected O2D_PropWidget(Func<object?> get, Action<object?> set)
    {
        _get = get; _set = set;
        mouse_filter        = EMouseFilter.Stop;
        size_flags_h        = ESizeFlags.ExpandFill;
        custom_minimum_size = new Vector2(0, 22);
    }
}

// =====================================================================================================================
// Bool
// =====================================================================================================================

internal class O2DPropWidgetBool : O2D_PropWidget
{
    public O2DPropWidgetBool(Func<object?> g, Action<object?> s) : base(g, s) { }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (_rect.size.X <= 0) return;
        ImGui.SetCursorScreenPos(_rect.position);
        bool v = _get() is bool b && b;
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        if (ImGui.Checkbox("##", ref v)) _set(v);
        ImGui.PopID();
    }
}

// =====================================================================================================================
// Numerics — delegate to O2D_Number's ImGui DragFloat
// =====================================================================================================================

internal class O2DPropWidgetInt : O2D_Number
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
        value = _get() is int i ? i : 0;
        base.OnDraw(rhi, dt);
    }
}

internal class O2DPropWidgetFloat : O2D_Number
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
        if (!_dragging) value = _get() is float f ? f : 0f;
        base.OnDraw(rhi, dt);
    }
}

internal class O2DPropWidgetDouble : O2D_Number
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
        if (!_dragging) value = _get() is double d ? d : 0.0;
        base.OnDraw(rhi, dt);
    }
}

// =====================================================================================================================
// String
// =====================================================================================================================

internal class O2DPropWidgetStr : O2D_TextEdit
{
    private readonly Func<object?> _get;

    public O2DPropWidgetStr(Func<object?> get, Action<object?> set)
    {
        _get          = get;
        text          = get()?.ToString() ?? "";
        OnTextChanged = s => set(s);
        size_flags_h  = ESizeFlags.ExpandFill;
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (!focused) text = _get()?.ToString() ?? "";
        if (_rect.size.X <= 0) return;

        ImGui.SetCursorScreenPos(_rect.position);
        ImGui.SetNextItemWidth(_rect.size.X);
        string buf = text ?? "";
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        if (ImGui.InputText("##", ref buf, 1024))
        {
            text = buf;
            OnTextChanged?.Invoke(text);
        }
        ImGui.PopID();
    }
}

// =====================================================================================================================
// Generic label fallback
// =====================================================================================================================

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
        if (_rect.size.X <= 0) return;
        string txt = _get()?.ToString() ?? "(null)";
        var dl = ImGui.GetWindowDrawList();
        var ts = ImGui.CalcTextSize(txt);
        dl.AddText(new Vector2(_rect.position.X + 6, _rect.position.Y + (_rect.size.Y - ts.Y) * 0.5f),
            ToU32(Color.FromArgb(140, 140, 150)), txt);
    }

    private static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}

// =====================================================================================================================
// Color
// =====================================================================================================================

internal class O2DPropWidgetColor : O2D_PropWidget
{
    public O2DPropWidgetColor(Func<object?> g, Action<object?> s) : base(g, s) { }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (_rect.size.X <= 0) return;
        var c  = _get() is Color col ? col : Color.White;
        var v4 = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        ImGui.SetCursorScreenPos(_rect.position);
        ImGui.SetNextItemWidth(_rect.size.X);
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        if (ImGui.ColorEdit4("##", ref v4, ImGuiColorEditFlags.NoLabel))
            _set(Color.FromArgb((int)(v4.W * 255), (int)(v4.X * 255), (int)(v4.Y * 255), (int)(v4.Z * 255)));
        ImGui.PopID();
    }
}

// =====================================================================================================================
// Enum
// =====================================================================================================================

internal class O2DPropWidgetEnum : O2D_PropWidget
{
    private readonly Array _values;

    public O2DPropWidgetEnum(Type enumType, Func<object?> g, Action<object?> s) : base(g, s)
        => _values = Enum.GetValues(enumType);

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (_rect.size.X <= 0) return;
        var cur     = _get();
        string preview = cur?.ToString() ?? "?";
        ImGui.SetCursorScreenPos(_rect.position);
        ImGui.SetNextItemWidth(_rect.size.X);
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        if (ImGui.BeginCombo("##", preview))
        {
            foreach (var val in _values)
            {
                bool sel = val?.Equals(cur) ?? false;
                if (ImGui.Selectable(val?.ToString() ?? "", sel)) _set(val);
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
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
// Vector2 / Vector3 / Quaternion
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
                    var q = get() is Quaternion qv ? qv : Quaternion.Identity;
                    var e = Quat_ToEuler(q);
                    return (object)(axis == 0 ? e.X : axis == 1 ? e.Y : e.Z);
                },
                sv =>
                {
                    var q  = get() is Quaternion qv ? qv : Quaternion.Identity;
                    var e  = Quat_ToEuler(q);
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

    private static Vector3 Quat_ToEuler(Quaternion q)
    {
        float roll  = MathF.Atan2(2f * (q.W * q.X + q.Y * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        float sinp  = 2f * (q.W * q.Y - q.Z * q.X);
        float pitch = MathF.Abs(sinp) >= 1f ? MathF.CopySign(MathF.PI * 0.5f, sinp) : MathF.Asin(sinp);
        float yaw   = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.Y * q.Y + q.Z * q.Z));
        const float R2D = 180f / MathF.PI;
        return new Vector3(roll * R2D, pitch * R2D, yaw * R2D);
    }

    private static Quaternion Quat_FromEuler(Vector3 deg)
    {
        const float D2R = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(deg.Z * D2R, deg.Y * D2R, deg.X * D2R);
    }
}
