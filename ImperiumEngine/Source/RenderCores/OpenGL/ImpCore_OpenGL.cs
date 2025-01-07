using System.Diagnostics;
using System.Runtime.CompilerServices;
using ImperiumEngine.Source.Cores;
using ImperiumEngine.Source._temps;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using ImGuiNET;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ImperiumEngine.Source.Renderers;

public class ImpCore_OpenGL : ImpCore
{
    public override void Window_Create()
    {
        using (ImpGL_Window game = new ImpGL_Window())
        {
            game.owner_core = this;
            game.Run();
        }
    }
}


internal class ImpGL_Window : GameWindow
{
    ImGuiController _controller;
    public ImpCore_OpenGL owner_core;
    
    public ImpGL_Window() : base(GameWindowSettings.Default, new NativeWindowSettings(){ Size = new Vector2i(1600, 900), APIVersion = new Version(3, 3) })
    { }

    protected override void OnLoad()
    {
        base.OnLoad();

        //Title += ": OpenGL Version: " + GL.GetString(StringName.Version);
        Title = ImpConfig.AppName;

        _controller = new ImGuiController(ClientSize.X, ClientSize.Y);
    }
        
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        // Update the opengl viewport
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

        // Tell ImGui of the new size
        _controller.WindowResized(ClientSize.X, ClientSize.Y);
    }
    
    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        owner_core.Core_Update(e.Time);
        base.OnUpdateFrame(e);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        _controller.Update(this, (float)e.Time);

        GL.ClearColor(new Color4(0, 32, 48, 255));
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        // Enable Docking
        ImGui.DockSpaceOverViewport();

        owner_core.Core_Draw(e.Time);
        
        
        
        _controller.Render();

        ImGuiController.CheckGLError("End of frame");

        SwapBuffers();
        
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
            
            
        _controller.PressChar((char)e.Unicode);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
            
        _controller.MouseScroll(e.Offset);
    }

}

