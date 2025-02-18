using System;
using System.Drawing;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace ImperiumEngine.Source.RHI;

public class ImpRHI_OpenGL : ImpRHI_SILK
{
    public ImpRHI_OpenGL(string title, Vector2 windowSize) : base(title, windowSize) { }
    
    private GL _gl;
    protected override void On_PreBegin()
    {
        _gl = SilkWindow.CreateOpenGL();
        SilkInput = SilkWindow.CreateInput();
        SilkImgui = new ImGuiController(_gl, SilkWindow, SilkInput);
        base.On_PreBegin();
    }

    protected override void On_PreRender(double delta)
    {
        _gl.ClearColor(Color.FromArgb(255, (int) (.45f * 255), (int) (.55f * 255), (int) (.60f * 255)));
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public override void Window_Resize(Vector2 size)
    {
        _gl.Viewport(new Vector2D<int>((int)size.X,(int)size.Y));
    }

    public override void SILK_End()
    {
        _gl.Dispose();
        base.SILK_End();
    }
    
}