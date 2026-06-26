using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

// A small grip widget that starts a drag on mouse-press.
// Place it as a TreeItem.left_widget (or any child) to make a row draggable.
public class O2D_DragHandle : ImpComponent2D
{
    private readonly object? _payload;

    private static readonly Color C_Normal  = Color.FromArgb(65,  65,  75);
    private static readonly Color C_Hovered = Color.FromArgb(130, 130, 150);
    private static readonly Color C_Active  = Color.FromArgb(100, 160, 255);

    public A_Texture2D? t_ico_handle;

    public O2D_DragHandle(object? payload)
    {
        t_ico_handle = ImpAsset.Load<A_Texture2D>("T_ico_DragHandle");

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
        bool active = ImpApp.Active?.players.Count > 0
                      && ImpApp.Active.players[0].DragSource == this;

        var color = active ? C_Active : hovered ? C_Hovered : C_Normal;

        if (t_ico_handle == null)
            return;

        const int iconSize = 14;

        float x = _rect.position.X + (_rect.size.X - iconSize) * 0.5f;
        float y = _rect.position.Y + (_rect.size.Y - iconSize) * 0.5f;

        rhi.Draw2D_Texture(
            t_ico_handle,
            new TVector2i((int)x, (int)y),
            new TVector2i(iconSize, iconSize),
            color);
    }
}