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
using Silk.NET.Input;

namespace ImperiumEngine.Objects._2D;

public class O2D_Number : ImpComponent2D
{
    public A_UiStyle_Spinner? style;

    public double  value;
    public double  step     = 1.0;
    public int     decimals = 3;
    public string? suffix;

    public Color?  band_color;
    public string? band_label;
    protected const float BandW = 16f;

    public Action<double>? OnValueChanged;

    // Kept so subclasses can read (ImGui owns drag/edit interaction now)
    protected bool _dragging;

    public O2D_Number()
    {
        mouse_filter        = EMouseFilter.Stop;
        custom_minimum_size = new Vector2(0, 24);
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (_rect.size.X <= 0 || _rect.size.Y <= 0) return;
        var pos  = _rect.position;
        var size = _rect.size;
        var dl   = ImGui.GetWindowDrawList();

        float bandOffset = 0f;
        if (band_color.HasValue)
        {
            dl.AddRectFilled(pos + new Vector2(1, 1),
                pos + new Vector2(BandW + 1, size.Y - 1),
                ToU32(band_color.Value));

            if (!string.IsNullOrEmpty(band_label))
            {
                var ts = ImGui.CalcTextSize(band_label);
                dl.AddText(pos + new Vector2(1 + (BandW - ts.X) * 0.5f, (size.Y - ts.Y) * 0.5f),
                    0xFFFFFFFF, band_label);
            }
            bandOffset = BandW + 1;
        }

        string fmt = decimals == 0 ? "%.0f" : $"%.{Math.Clamp(decimals, 1, 6)}f";
        if (!string.IsNullOrEmpty(suffix)) fmt += suffix;

        float fv = (float)value;
        ImGui.SetCursorScreenPos(pos + new Vector2(bandOffset, 0));
        ImGui.SetNextItemWidth(size.X - bandOffset);
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        if (ImGui.DragFloat("##", ref fv, (float)(step * 0.1), float.MinValue, float.MaxValue, fmt))
        {
            value = fv;
            OnValueChanged?.Invoke(value);
        }
        ImGui.PopID();
    }

    private static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}

public class A_UiStyle_Spinner : ImpAsset
{
    public Color color_bg        = Color.FromArgb(20, 20, 24);
    public Color color_bg_hover  = Color.FromArgb(30, 30, 36);
    public Color color_bg_active = Color.FromArgb(16, 16, 20);
    public Color color_text      = Color.FromArgb(210, 210, 222);
    public Color color_drag_fill = Color.FromArgb(40, 80, 140, 200);
    public Color color_border    = Color.FromArgb(75, 135, 245);
    public Color color_cursor    = Color.FromArgb(160, 200, 255);
}
