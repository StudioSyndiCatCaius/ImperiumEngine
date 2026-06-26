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

public class O2D_DragHandle : ImpComponent2D
{
    private readonly object? _payload;

    private static readonly Color C_Normal  = Color.FromArgb(65,  65,  75);
    private static readonly Color C_Hovered = Color.FromArgb(130, 130, 150);
    private static readonly Color C_Active  = Color.FromArgb(100, 160, 255);

    public A_Texture2D? t_ico_handle;

    public O2D_DragHandle(object? payload)
    {
        t_ico_handle        = ImpAsset.Load<A_Texture2D>("T_ico_DragHandle");
        _payload            = payload;
        mouse_filter        = EMouseFilter.Stop;
        custom_minimum_size = new Vector2(20, 0);
    }

    public override void OnMousePressed(int button)
    {
        if (button == 0 && ImpApp.Active?.players.Count > 0)
            ImpApp.Active.players[0].Drag_Begin(this, _payload);
    }

    public override void OnDraw(ImpRender rhi, double dt)
    {
        bool active = ImpApp.Active?.players.Count > 0 && ImpApp.Active.players[0].DragSource == this;
        var color = active ? C_Active : hovered ? C_Hovered : C_Normal;

        var dl = ImGui.GetWindowDrawList();
        const float iconSize = 14f;
        float x = _rect.position.X + (_rect.size.X - iconSize) * 0.5f;
        float y = _rect.position.Y + (_rect.size.Y - iconSize) * 0.5f;

        if (t_ico_handle != null)
        {
            rhi.Draw2D_Image(t_ico_handle, new Vector2(x, y), new Vector2(iconSize, iconSize), color);
            return;
        }

        // Fallback grip dots
        for (int i = 0; i < 3; i++)
        {
            float ry = y + i * 4f;
            dl.AddRectFilled(new Vector2(x + 2, ry + 2), new Vector2(x + iconSize - 2, ry + 4), ToU32(color));
        }
    }

    private static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}
