using System.Drawing;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects._1D;

public class O1D_Viewport : ImpComponent
{
    public List<ImpLevel> drawnLevels = new();
    public A_RenderTarget? renderTarget;

    public List<ImpCamera> renderCameras = new();

    public Color backgroundColor = Color.Black;
    public bool drawGrid = true;

    // set each frame by the hosting 2D component before OnDraw fires
    public int width;
    public int height;

    public O1D_Viewport()
    {
        renderCameras.Add(new ImpCamera());
    }

    protected  override void OnDraw(ImpRender rhi, double dt)
    {
        if (renderTarget == null || width <= 0 || height <= 0) return;

        rhi.RT_Viewport_Begin(renderTarget, width, height);

        foreach (var camera in renderCameras)
        {
            rhi.Viewport_SetCamera(camera, width, height);

            foreach (var level in drawnLevels)
            {
                foreach (var o in level.Objects)
                {
                    if (o is ImpComponent3D c3D)
                        c3D.OnDraw3D(rhi, this, camera, dt);
                }
            }
        }

        rhi.RT_Viewport_End(renderTarget);
    }
}
