using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public class O2D_Text : ImpComponent2D
{
    [ImpVar][Exposed] public string text;
    [ImpVar][Exposed] public bool wrap     = true;
    [ImpVar][Exposed] public bool isRichText;

    [ImpVar][Exposed] public A_UiStyle_Text style = new A_UiStyle_Text();

    public O2D_Text() => mouse_filter = EMouseFilter.Ignore;

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dl = ImGui.GetWindowDrawList();
        dl.AddText(_rect.position, ToU32(style.color), text);
    }

    public override Vector2 Size_GetMinimum()
    {
        var own = base.Size_GetMinimum();
        if (string.IsNullOrEmpty(text)) return own;
        var ts    = ImGui.CalcTextSize(text);
        float def = ImGui.GetFontSize();
        float scale = def > 0 ? style.font_size / def : 1f;
        return Vector2.Max(new Vector2(ts.X * scale, ts.Y * scale), own);
    }

    internal static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}

public class A_UiStyle_Text : ImpAsset
{
    public Color color     = Color.White;
    public int   font_size = 16;

    public bool      use_outline;
    public Color     outline_Color;
    public float     outline_thickness;

    public bool      use_shadow;
    public TVector2i shadow_offset;
    public float     shadow_sharpness;
}
