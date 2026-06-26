using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public enum ETabBarLayout : byte { Horizontal, Vertical }

public struct TTabBarOption { public string text; }

public class O2D_TabBar : ImpComponent2D
{
    public A_UiStyle_TabBar style;
    public ETabBarLayout    layout;
    public List<TTabBarOption> options = new();
    public Action<int>      OnSelect;
    public int              activeIndex = 0;

    // Internal layout helpers — not added to children, just track rects
    private readonly List<O2D_Button> _buttons = new();
    private const float TabMinWidth = 90f;

    public void Option_Add(string label)
    {
        options.Add(new TTabBarOption { text = label });
        int index = _buttons.Count;
        var btn = new O2D_Button { text = label };
        btn.custom_minimum_size = new Vector2(TabMinWidth, 0);
        _buttons.Add(btn);
        // NOT added to _children — O2D_TabBar.OnDraw handles all tab rendering
    }

    protected override void Layout_Children(TRect2 rect)
    {
        bool horizontal = layout == ETabBarLayout.Horizontal;
        float pos = horizontal ? rect.position.X : rect.position.Y;
        foreach (var btn in _buttons)
        {
            float w = MathF.Max(btn.Size_GetMinimum().X, TabMinWidth);
            btn.Layout_SetRect(horizontal
                ? new TRect2(new Vector2(pos, rect.position.Y), new Vector2(w, rect.size.Y))
                : new TRect2(new Vector2(rect.position.X, pos), new Vector2(rect.size.X, btn.Size_GetMinimum().Y)));
            pos += horizontal ? w : btn.Size_GetMinimum().Y;
        }
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        var dl = ImGui.GetWindowDrawList();

        var bgCol = style?.color_bar_background ?? Color.FromArgb(30, 30, 34);
        dl.AddRectFilled(_rect.position, _rect.position + _rect.size, ToU32(bgCol));

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,  Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);

        for (int i = 0; i < _buttons.Count && i < options.Count; i++)
        {
            var br   = _buttons[i].Rect_Get();
            if (br.size.X <= 0) continue;

            bool active = i == activeIndex;
            var fg  = active ? Color.White : Color.FromArgb(160, 160, 170);
            var bg  = Color.Transparent;
            var bgH = Color.FromArgb(45, 45, 50);
            var bgP = Color.FromArgb(20, 20, 24);

            ImGui.SetCursorScreenPos(br.position);
            ImGui.PushStyleColor(ImGuiCol.Button,        ToVec4(bg));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ToVec4(bgH));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  ToVec4(bgP));
            ImGui.PushStyleColor(ImGuiCol.Text,          ToVec4(fg));
            ImGui.PushID(i + 1000);
            if (ImGui.Button(options[i].text, br.size))
                OnSelect?.Invoke(i);
            ImGui.PopID();
            ImGui.PopStyleColor(4);
        }

        ImGui.PopStyleVar(2);

        // Active indicator strip
        if (activeIndex >= 0 && activeIndex < _buttons.Count)
        {
            var br = _buttons[activeIndex].Rect_Get();
            if (br.size.X > 0)
            {
                var indCol = style?.color_active_indicator ?? Color.FromArgb(100, 150, 255);
                dl.AddRectFilled(
                    new Vector2(br.position.X,             br.position.Y + br.size.Y - 3),
                    new Vector2(br.position.X + br.size.X, br.position.Y + br.size.Y),
                    ToU32(indCol));
            }
        }
    }

    private static uint    ToU32(Color c)  => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
    private static Vector4 ToVec4(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}

// =====================================================================================================================

public struct TTabButtonStyle
{
    public Color color_background;
    public Color color_content;
    public float border_radius;

    public TTabButtonStyle(Color bg, Color content, float radius = 0f)
    {
        color_background = bg;
        color_content    = content;
        border_radius    = radius;
    }
}

public class A_UiStyle_TabBar : ImpAsset
{
    public Color color_bar_background   = Color.FromArgb(30, 30, 34);
    public Color color_active_indicator = Color.FromArgb(100, 150, 255);

    public TTabButtonStyle Style_Inactive       = new(Color.Transparent,           Color.FromArgb(160, 160, 170));
    public TTabButtonStyle Style_Active         = new(Color.Transparent,           Color.White);
    public TTabButtonStyle Style_Inactive_Hover = new(Color.FromArgb(45, 45, 50), Color.FromArgb(200, 200, 210));
    public TTabButtonStyle Style_Active_Hover   = new(Color.FromArgb(45, 45, 50), Color.White);
    public TTabButtonStyle Style_Pressed        = new(Color.FromArgb(20, 20, 24), Color.White);
}
