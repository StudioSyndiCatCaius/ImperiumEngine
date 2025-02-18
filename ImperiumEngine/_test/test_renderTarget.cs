using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using ImGuiNET;
using System.Drawing;
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL.Extensions.ImGui;

public class ProgramOLD
{
    private static IWindow window;
    private static GL gl;
    private static IInputContext input;
    private static ImGuiController imGuiController;
    
    // Render target 
    private static uint frameBuffer;
    private static uint renderTexture;
    private static uint depthBuffer;
    private static int renderWidth = 800;
    private static int renderHeight = 600;

    private static void MainZ(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Render Target Demo";
        
        window = Window.Create(options);
        
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Closing += OnClose;
        
        window.Run();
    }

    private static unsafe void OnLoad()
    {
        gl = window.CreateOpenGL();
        input = window.CreateInput();
        imGuiController = new ImGuiController(gl, window, input);
        scene = new TestSceneo(gl);

        // Create framebuffer and texture
        frameBuffer = gl.CreateFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);

        renderTexture = gl.CreateTexture(TextureTarget.Texture2D);
        gl.BindTexture(TextureTarget.Texture2D, renderTexture);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 
            (uint)renderWidth, (uint)renderHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

        // Create depth BufferData
        depthBuffer = gl.CreateRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthBuffer);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24,
            (uint)renderWidth, (uint)renderHeight);

        // Attach texture and depth BufferData to framebuffer
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, renderTexture, 0);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, depthBuffer);

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new Exception("Framebuffer is not complete!");
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private static void OnUpdate(double deltaTime)
    {
        imGuiController.Update((float)deltaTime);
    }

    private static void OnRender(double deltaTime)
    {
        // Render 3D content to texture
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
        gl.Viewport(0, 0, (uint)renderWidth, (uint)renderHeight);
        gl.ClearColor(Color.CornflowerBlue);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        // Your 3D rendering code here
        Render3DContent();

        // Switch back to default framebuffer for ImGui
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);
        gl.ClearColor(Color.DarkGray);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        // ImGui window with render texture
        ImGui.Begin("3D View");
        
        // Get the current window content region
        var windowSize = ImGui.GetContentRegionAvail();
        
        // Calculate aspect ratio preserving size
        float aspectRatio = (float)renderWidth / renderHeight;
        Vector2 displaySize = new Vector2(
            Math.Min(windowSize.X, windowSize.Y * aspectRatio),
            Math.Min(windowSize.Y, windowSize.X / aspectRatio)
        );

        // Display the render texture
        ImGui.Image(new IntPtr(renderTexture), displaySize);
        
        ImGui.End();

        imGuiController.Render();
    }

    private static TestSceneo scene;

    private static void Render3DContent()
    {
        scene?.Render(window.Time);
    }

    private static void OnClose()
    {
        scene?.Dispose();
        gl.DeleteFramebuffer(frameBuffer);
        gl.DeleteTexture(renderTexture);
        gl.DeleteRenderbuffer(depthBuffer);
        imGuiController.Dispose();
        gl.Dispose();
    }
}
// ==========================================================================================================================================================
public class TestSceneo : IDisposable
{
    private readonly GL gl;
    private uint vao;
    private uint vbo;
    private uint ebo;
    private uint shader;
    private float rotation = 0.0f;

    private static readonly float[] Vertices =
    {
        // Front
        -0.5f, -0.5f,  0.5f,    1.0f, 0.0f, 0.0f,
         0.5f, -0.5f,  0.5f,    0.0f, 1.0f, 0.0f,
         0.5f,  0.5f,  0.5f,    0.0f, 0.0f, 1.0f,
        -0.5f,  0.5f,  0.5f,    1.0f, 1.0f, 0.0f,
        // Back
        -0.5f, -0.5f, -0.5f,    1.0f, 0.0f, 1.0f,
         0.5f, -0.5f, -0.5f,    0.0f, 1.0f, 1.0f,
         0.5f,  0.5f, -0.5f,    1.0f, 1.0f, 1.0f,
        -0.5f,  0.5f, -0.5f,    0.0f, 0.0f, 0.0f,
    };

    private static readonly uint[] Indices =
    {
        // Front
        0, 1, 2,
        2, 3, 0,
        // Top
        3, 2, 6,
        6, 7, 3,
        // Right
        1, 5, 6,
        6, 2, 1,
        // Left
        4, 0, 3,
        3, 7, 4,
        // Back
        5, 4, 7,
        7, 6, 5,
        // Bottom
        4, 5, 1,
        1, 0, 4
    };

    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;
        
        out vec3 fragColor;
        
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
            fragColor = aColor;
        }";

    private const string FragmentShaderSource = @"
        #version 330 core
        in vec3 fragColor;
        out vec4 FragColor;
        
        void main()
        {
            FragColor = vec4(fragColor, 1.0);
        }";

    public TestSceneo(GL gl)
    {
        this.gl = gl;
        InitializeBuffers();
        CreateShaderProgram();
    }

    private unsafe void InitializeBuffers()
    {
        vao = gl.CreateVertexArray();
        gl.BindVertexArray(vao);

        vbo = gl.CreateBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (void* v = &Vertices[0])
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
        }

        ebo = gl.CreateBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (void* i = &Indices[0])
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
    }

    private void CreateShaderProgram()
    {
        uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vertexShader, VertexShaderSource);
        gl.CompileShader(vertexShader);

        uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fragmentShader, FragmentShaderSource);
        gl.CompileShader(fragmentShader);

        shader = gl.CreateProgram();
        gl.AttachShader(shader, vertexShader);
        gl.AttachShader(shader, fragmentShader);
        gl.LinkProgram(shader);

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
    }

    public unsafe void Render(double deltaTime)
    {
        rotation += (float)deltaTime;

        gl.Enable(EnableCap.DepthTest);
        gl.UseProgram(shader);
        gl.BindVertexArray(vao);

        var model = Matrix4x4.CreateRotationY(rotation) * Matrix4x4.CreateRotationX(rotation * 0.5f);
        var view = Matrix4x4.CreateTranslation(0, 0, -3);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)Math.PI / 4,
            800f / 600f,  // Aspect ratio
            0.1f,         // Near plane
            100.0f        // Far plane
        );

        gl.UniformMatrix4(gl.GetUniformLocation(shader, "model"), 1, false, (float*)&model);
        gl.UniformMatrix4(gl.GetUniformLocation(shader, "view"), 1, false, (float*)&view);
        gl.UniformMatrix4(gl.GetUniformLocation(shader, "projection"), 1, false, (float*)&projection);

        gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Length, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
        gl.DeleteProgram(shader);
    }
}