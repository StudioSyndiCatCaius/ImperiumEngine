using System.Drawing;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public class O2D_Image : ImpComponent2D
{
    public A_Texture? _texture;

    protected  override void OnDraw(ImpRender rhi, double dt)
    {
        if (_texture == null) return;

        var pos  = TVector2i.From(_rect.position);
        var size = TVector2i.From(_rect.size);

        if (size.x <= 0 || size.y <= 0) return;

        if (_texture is A_RenderTarget rt)
            rhi.Draw2D_RenderTarget(rt, pos, size);
        else if (_texture is A_Texture2D tex2D)
            rhi.Draw2D_Texture(tex2D, pos, size, Color.White);
    }
}
