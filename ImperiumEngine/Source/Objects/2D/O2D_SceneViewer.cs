
using System.Drawing;
using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Source.Libs;
using ImperiumEngine.Source.Objects._3D;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

using static ImperiumEngine.Source.Libs.ImpLib_Math;

namespace ImperiumEngine.Source.Objects._2D;

public class O2D_SceneViewer : ImpComponent2D
{

    private GL              _gl    = Program.gl;
    private ImGuiController _imGui = Program.imgui;
    
    // Render target 
    private static uint frameBuffer;
    private static uint renderTexture;
    private static uint depthBuffer;
    private static int  _renderX = 800;
    private static int  _renderY = 600;
    
    //shader
    private        uint    shader;
    
    private float rotation = 0.0f;
    
    private        Vector2 _windowSize;
    
    private Color _colorBkg;
    
    public override void On_Begin()
    {
        _colorBkg = Vec4_ToColor(Vector4.One);
        //_sceneContext = new SceneContext(_gl);
        
        // -----------------------------------------------------------------------------------------------------
        //CREATE SHADER
        // -----------------------------------------------------------------------------------------------------
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, ImpLib_3D.GetDefaultShader_Vertex());
        _gl.CompileShader(vertexShader);

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, ImpLib_3D.GetDefaultShader_Fragment());
        _gl.CompileShader(fragmentShader);
        
        string infoLog = _gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Vertex shader compilation error: {infoLog}");
        }

        shader = _gl.CreateProgram();
        
        _gl.AttachShader(shader, vertexShader);
        _gl.AttachShader(shader, fragmentShader);
        _gl.LinkProgram(shader);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        var a = new O3D_Mesh(ImpLib_File.GetPath_EngineContent()+"meshes/mid/wall.glb",ImpLib_File.GetPath_EngineContent()+"textures/silk.png");
        Add_Child(a);
        
        
        // -----------------------------------------------------------------------------------------------------
        // GENERATE BUFFERS for Render Target
        // -----------------------------------------------------------------------------------------------------
        unsafe
        {
            // Create framebuffer and texture
            frameBuffer = _gl.CreateFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);

            renderTexture = _gl.CreateTexture(TextureTarget.Texture2D);
            _gl.BindTexture(TextureTarget.Texture2D, renderTexture);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 
                (uint)_renderX, (uint)_renderY, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            // Create depth BufferData
            depthBuffer = _gl.CreateRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthBuffer);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24,
                (uint)_renderX, (uint)_renderY);

            // Attach texture and depth BufferData to framebuffer
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, renderTexture, 0);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, depthBuffer);

            if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            {
                throw new Exception("Framebuffer is not complete!");
            }

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
            base.On_Begin();
        }
    }

    public override void On_Update(float delta)
    {
        base.On_Update(delta);
    }
    
    public override void On_Draw(float delta)
    {
        // ------------------------------------------------------------------------------------------
        // Draw Scene
        // ------------------------------------------------------------------------------------------

        //_renderX = (int)_windowSize.X;
        //_renderY = (int)_windowSize.Y;
        
        // Render 3D content to texture
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
        _gl.Viewport(0, 0, (uint)_renderX, (uint)_renderY);
        _gl.ClearColor(_colorBkg);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        // Your 3D rendering code here
        Vector2 screenSize = new Vector2(_renderX, _renderY);
        
        // ------------------------------------------------------------------------------------------
        // Render Scene
        // ------------------------------------------------------------------------------------------
        
        rotation += delta;

        _gl.Enable(EnableCap.DepthTest);
        _gl.UseProgram(shader);
        
        var view = Matrix4x4.CreateTranslation(0, 0, -3);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)Math.PI / 4,
            screenSize.X / screenSize.Y,  // Aspect ratio
            0.1f,         // Near plane
            100.0f        // Far plane
        );

        unsafe
        {
            _gl.UniformMatrix4(_gl.GetUniformLocation(shader, "view"), 1, false, (float*)&view);
            _gl.UniformMatrix4(_gl.GetUniformLocation(shader, "projection"), 1, false, (float*)&projection);
            
            
            foreach (var c in GetChildren())
            {
                if (c is ImpComponent3D c3d)
                {
                    c3d.On_Draw3D(delta,shader);
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Draw ImGui
        // ------------------------------------------------------------------------------------------
        
        // ImGui _window with render texture
        ImGui.Begin("3D View");
        
        // Get the current _window content region
        _windowSize = ImGui.GetContentRegionAvail();
        
        // Calculate aspect ratio preserving size
        float aspectRatio = (float)_renderX / _renderY; 
        Vector2 displaySize = new Vector2( Math.Min(_windowSize.X, _windowSize.Y * aspectRatio),  Math.Min(_windowSize.Y, _windowSize.X / aspectRatio) );
      // Vector2 displaySize = new Vector2( _windowSize.X,_windowSize.Y );
        // Display the render texture
        ImGui.Image(new IntPtr(renderTexture), displaySize);
        
    //    Vector4 bkgColor = Color_ToVec4(_colorBkg);
      //  ImGui.ColorPicker4("background color", ref bkgColor);
       // _colorBkg=Vec4_ToColor(bkgColor);
       
        ImGui.End();
        
        
        // ------------------------------------------------------------------------------------------
        // END
        // ------------------------------------------------------------------------------------------
        base.On_Draw(delta);
    }
    
    public override void On_End()
    {
        _gl.DeleteFramebuffer(frameBuffer);
        _gl.DeleteTexture(renderTexture);
        _gl.DeleteRenderbuffer(depthBuffer);
        _imGui.Dispose();
        _gl.Dispose();
        
        base.On_End();
    }
}

// =============================================================================================================================================================================
// SCENE CONTEXT
// =============================================================================================================================================================================

