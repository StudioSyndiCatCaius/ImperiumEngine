using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL.Extensions.ImGui;
using ImGuiNET;
using System.Drawing;
using System.Numerics;
using ImperiumEngine.Source;
using ImperiumEngine.Source.Libs;
using ImperiumEngine.Source.Objects._2D;

namespace ImperiumEngine;

public class Program
{
    public static IWindow         window;
    public static  GL              gl;
    public static  IInputContext   input;
    public static  ImGuiController imgui;
    private static ImpObject       object_root;

    static void Main(string[] args)
    {
        // ------------------------------------------------------------------------------
        // CREATE WINDOW
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Imperium Engine";
        
        window = Window.Create(options);
        
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Resize += OnResize;
        window.Closing += OnClose;
        
        window.Run();
        window.Dispose();
    }

    private static void OnLoad()
    {
        imgui = new ImGuiController(
            gl = window.CreateOpenGL(), 
            window, 
            input = window.CreateInput());
        
        //INITIALIZE ROOT OBJECT
        object_root = new ImpObject(); 
        object_root.Native_Begin();
        
        //TESTING SCENE ============
        object_root.Add_Child(new O2D_SceneViewer());
    }
    
    private static void OnResize(Vector2D<int> s)
    {
        gl.Viewport(s);
    }

    private static void OnUpdate(double deltaTime)
    {
        object_root.Native_Update((float)deltaTime);
    }

    private static void OnRender(double deltaTime)
    {
        imgui.Update((float)deltaTime);
        
        object_root.Native_Draw((float)deltaTime);
        
        // Switch back to default framebuffer for ImGui
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);
        gl.ClearColor(Color.DarkGray);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        
        ImGui.ShowDemoWindow();
        
        imgui.Render();
    }

    private static void OnClose()
    {
        imgui?.Dispose();
        input?.Dispose();
        gl?.Dispose();
        object_root.Dispose();
    }
}
