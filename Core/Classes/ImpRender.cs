using System.Drawing;
using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

// Baseclass for rendering and RHIs (Vulkan, OpenGL, DirectX, ...). ALL engine
// graphics calls go through this class so the backend can be swapped per-platform.
public class ImpRender
{
    public ImpApp? App;
    public TVector2i ViewportSize;

    // Active app's RHI, reachable from anywhere.
    public static ImpRender Get()
    {
        ImpApp app = ImpApp.Active
            ?? throw new InvalidOperationException(
                "ImpRender.Get() called before an ImpApp exists. Create an ImpApp first.");
        return app.RHI
            ?? throw new InvalidOperationException(
                "The active ImpApp has no RHI assigned.");
    }

    // ================================================================================================================
    // LIFECYCLE
    // ================================================================================================================
    public virtual void OnRHI_Init() {}
    public virtual void OnRHI_Update(double dt) {}
    public virtual void OnRHI_Draw(double dt) { }
    public virtual void OnRHI_End() {}

    // Wrap each rendered frame (backends open/close their draw context / swapchain here).
    public virtual void OnRHI_FrameBegin(double dt) {}
    public virtual void OnRHI_FrameEnd(double dt) {}

    // ================================================================================================================
    // 2D
    // ================================================================================================================
    public virtual void Draw2D_Text(TVector2i pos, string text, int size, Color color) {}
    public virtual void Draw2D_Rect(TVector2i pos, TVector2i size, Color color) {}
    public virtual void Draw2D_RectRounded(TVector2i pos, TVector2i size, Color color, float radius) {}
    
    public virtual void Draw2D_RectComplex(TVector2i pos, TVector2i size, TComplexRectData data) {}
    public virtual void Draw2D_Texture(A_Texture2D texture, TVector2i pos, TVector2i size, Color tint) {}

    public virtual bool Draw2D_Button(TVector2i pos, TVector2i size, string text, Color color) { return false;}

    // pixel size a string would occupy at the given font size
    public virtual TVector2i Text_Measure(string text, int size) => new TVector2i(0, 0);

    // ================================================================================================================
    // 3D
    // ================================================================================================================
    public virtual bool Draw3D_Line(Vector3 start, Vector3 end, Color color, float thickness = 1) { return false; }

    public virtual bool Draw3D_DebugSphere(Vector3 start, Color color, float thickness = 1) { return false; }
    public virtual bool Draw3D_DebugBox(Vector3 start, Quaternion rot, Color color, float thickness = 1) { return false; }
    public virtual bool Draw3D_DebugCone(Vector3 start, Quaternion rot, Color color, float thickness = 1) { return false; }
    
    public virtual bool Draw3D_MeshFast(TTransform3D transform, TMeshModel mesh) { return false; }
}
