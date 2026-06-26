using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public enum EButtonIconPosition { Left, Right, Top, Bottom }

public class O2D_Button : ImpComponent2D
{
    public string text;
    public A_UiStyle_Button style = new A_UiStyle_Button();

    [ImpVar][Exposed] public int font_size = 16;

    public A_Texture2D icon;
    public Vector2 icon_size = new Vector2(16, 16);
    public EButtonIconPosition icon_position = EButtonIconPosition.Left;
    public float icon_separation = 6f;

    public Action<O2D_Button> OnPressed;
    public Action<O2D_Button> OnReleased;

    [ImpVar][Exposed] public Color color         = Color.FromArgb(70, 70, 76);
    [ImpVar][Exposed] public Color color_hover   = Color.FromArgb(92, 92, 100);
    [ImpVar][Exposed] public Color color_pressed = Color.FromArgb(50, 50, 56);
    [ImpVar][Exposed] public Color text_color    = Color.White;

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (_rect.size.X <= 0 || _rect.size.Y <= 0) return;
        var pos  = _rect.position;
        var size = _rect.size;

        bool hasText = !string.IsNullOrEmpty(text);
        bool hasIcon = icon != null;

        if (!hasIcon)
        {
            // Text-only: native ImGui.Button handles centering + hover/active colours
            ImGui.SetCursorScreenPos(pos);
            ImGui.PushStyleColor(ImGuiCol.Button,        ToVec4(color));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ToVec4(color_hover));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  ToVec4(color_pressed));
            ImGui.PushStyleColor(ImGuiCol.Text,          ToVec4(text_color));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,  Vector2.Zero);
            ImGui.PushID(RuntimeHelpers.GetHashCode(this));
            if (ImGui.Button(text ?? "", size)) OnPressed?.Invoke(this);
            ImGui.PopID();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(4);
            return;
        }

        // Icon (±text): InvisibleButton for interaction, manual draw for visuals
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushID(RuntimeHelpers.GetHashCode(this));
        bool clicked = ImGui.InvisibleButton("##", size);
        ImGui.PopID();
        if (clicked) OnPressed?.Invoke(this);

        bool isHov = ImGui.IsItemHovered();
        bool isAct = ImGui.IsItemActive();
        var  bg    = isAct ? color_pressed : isHov ? color_hover : color;
        var  dl    = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, ToU32(bg), 7f);

        if (hasIcon && !hasText)
        {
            var ip = pos + (size - icon_size) * 0.5f;
            rhi.Draw2D_Image(icon, ip, icon_size, text_color);
        }
        else // icon + text
        {
            var ts  = ImGui.CalcTextSize(text);
            bool hz = icon_position is EButtonIconPosition.Left or EButtonIconPosition.Right;
            var grp = hz
                ? new Vector2(icon_size.X + icon_separation + ts.X, MathF.Max(icon_size.Y, ts.Y))
                : new Vector2(MathF.Max(icon_size.X, ts.X), icon_size.Y + icon_separation + ts.Y);
            var origin = pos + (size - grp) * 0.5f;

            Vector2 iconPos, textPos;
            switch (icon_position)
            {
                case EButtonIconPosition.Right:
                    textPos = new Vector2(origin.X, origin.Y + (grp.Y - ts.Y) * 0.5f);
                    iconPos = new Vector2(origin.X + ts.X + icon_separation, origin.Y + (grp.Y - icon_size.Y) * 0.5f);
                    break;
                case EButtonIconPosition.Top:
                    iconPos = new Vector2(origin.X + (grp.X - icon_size.X) * 0.5f, origin.Y);
                    textPos = new Vector2(origin.X + (grp.X - ts.X) * 0.5f, origin.Y + icon_size.Y + icon_separation);
                    break;
                case EButtonIconPosition.Bottom:
                    textPos = new Vector2(origin.X + (grp.X - ts.X) * 0.5f, origin.Y);
                    iconPos = new Vector2(origin.X + (grp.X - icon_size.X) * 0.5f, origin.Y + ts.Y + icon_separation);
                    break;
                default: // Left
                    iconPos = new Vector2(origin.X, origin.Y + (grp.Y - icon_size.Y) * 0.5f);
                    textPos = new Vector2(origin.X + icon_size.X + icon_separation, origin.Y + (grp.Y - ts.Y) * 0.5f);
                    break;
            }
            rhi.Draw2D_Image(icon, iconPos, icon_size, text_color);
            dl.AddText(textPos, ToU32(text_color), text);
        }
    }

    public override Vector2 Size_GetMinimum()
    {
        var own = Vector2.Max(custom_minimum_size, Vector2.Zero);
        if (string.IsNullOrEmpty(text)) return own;
        var ts = ImGui.CalcTextSize(text);
        return Vector2.Max(new Vector2(ts.X + 16f, ts.Y + 8f), own);
    }

    public override void OnMouseReleased(int button) => OnReleased?.Invoke(this);

    private static uint   ToU32(Color c)  => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
    private static Vector4 ToVec4(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}

// =====================================================================================================================

public struct TButtonStateStyle
{
    public Color color_background = Color.Gray;
    public Color color_content    = Color.White;
    public TMargin2D border;
    public TMargin2D padding;
    public float border_radius = 7.0f;

    public TButtonStateStyle() { border = default; padding = default; }
}

public class A_UiStyle_Button : ImpAsset
{
    public TButtonStateStyle Style_Normal, Style_Hover, Style_Pressed;

    public A_UiStyle_Button()
    {
        Style_Normal  = new TButtonStateStyle { color_background = Color.FromArgb(70, 70, 76) };
        Style_Hover   = new TButtonStateStyle { color_background = Color.FromArgb(92, 92, 100) };
        Style_Pressed = new TButtonStateStyle { color_background = Color.FromArgb(50, 50, 56) };
    }
}
