using System.Drawing;
using System.Numerics;
using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

// Baseclass for rendering and RHIs (like Vulkan, OpenGL, DirectX)
public class ImpRender
{
    ImpApp App;
    
    public virtual void OnRHI_Init() {}
    public virtual void OnRHI_Update(double dt) {}
    public virtual void OnRHI_Draw(double dt) { }
    public virtual void OnRHI_End() {}
    
    // ================================================================================================================
    // 2D
    // ================================================================================================================
    public virtual void Draw2D_Text(TVector2i pos,string text, int size) {}
    public virtual void Draw2D_Rect(TVector2i pos, TVector2i size) {}
    
    public virtual bool Draw2D_Button(TVector2i pos, TVector2i size, string text, Color color) { return false;}
    
    // ================================================================================================================
    // 3D 
    // ================================================================================================================
    public virtual bool Draw3D_Line(Vector3 start, Vector3 end, Color color, float thickness = 1) { return false; }
    
    public virtual bool Draw3D_MeshFast(TTransform3D transform, TMeshModel mesh) { return false; }
    
}