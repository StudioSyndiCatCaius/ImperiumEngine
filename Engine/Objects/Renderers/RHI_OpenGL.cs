using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Structs;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace ImperiumEngine.Renderers;

// OpenGL 3.3 backend. All 2D drawing routes through ImGui's background draw list.
public class RHI_OpenGL : ImpRender
{
    private GL? _gl;
    private IInputContext? _input;

    // ImGui draw pipeline (custom renderer — no separate backend package needed)
    private uint _imProgram, _imVao, _imVbo, _imEbo;
    private int  _uImProj, _uImTex;

    private uint _fontTex;
    private uint _whiteTex;
    //private ImFontPtr _imFont;

    private readonly Dictionary<A_Texture2D, uint> _texCache = new();

    // Input state forwarded to ImGui each frame
    private Vector2 _mousePos;
    private readonly bool[] _mouseDown = new bool[3];
    private float _mouseScroll;
    private readonly List<char> _inputChars = new();

    // ================================================================================================================
    // SHADERS
    // ================================================================================================================

    private const string ImGuiVert = """
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUV;
        layout(location=2) in vec4 aColor;
        uniform mat4 uProj;
        out vec2 vUV;
        out vec4 vColor;
        void main() {
            vUV    = aUV;
            vColor = aColor;
            gl_Position = uProj * vec4(aPos, 0.0, 1.0);
        }
        """;

    private const string ImGuiFrag = """
        #version 330 core
        in  vec2 vUV;
        in  vec4 vColor;
        uniform sampler2D uTex;
        out vec4 FragColor;
        void main() { FragColor = vColor * texture(uTex, vUV); }
        """;

    // ================================================================================================================
    // LIFECYCLE
    // ================================================================================================================

    public override unsafe void OnRHI_Init()
    {
        var window = App?.Window ?? throw new InvalidOperationException("No window");
        _gl = window.CreateOpenGL();
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // ImGui draw pipeline — ImDrawVert is pos(8) + uv(8) + col(4) = 20 bytes
        _imProgram = Program_Build(ImGuiVert, ImGuiFrag);
        _uImProj   = _gl.GetUniformLocation(_imProgram, "uProj");
        _uImTex    = _gl.GetUniformLocation(_imProgram, "uTex");

        _imVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_imVao);
        _imVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _imVbo);
        _imEbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _imEbo);
        unsafe
        {
            const uint stride = 20u;
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float,        false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float,        false, stride, (void*)8);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true,  stride, (void*)16);
        }
        _gl.BindVertexArray(0);

        // 1×1 white fallback texture (used for solid-color draw cmds with no texture)
        _whiteTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
        unsafe { uint w = 0xFFFFFFFF; _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, &w); }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

        // Subscribe to the app's shared input context for ImGui IO
        _input = App!.Input;
        if (_input?.Mice.Count > 0)
        {
            var m = _input.Mice[0];
            m.MouseMove += (_, p)   => _mousePos = new Vector2(p.X, p.Y);
            m.MouseDown += (_, btn) => { if ((int)btn < 3) _mouseDown[(int)btn] = true;  };
            m.MouseUp   += (_, btn) => { if ((int)btn < 3) _mouseDown[(int)btn] = false; };
            m.Scroll    += (_, val) => _mouseScroll += val.Y;
        }
        if (_input.Keyboards.Count > 0)
            _input.Keyboards[0].KeyChar += (_, ch) => _inputChars.Add(ch);

        // ImGui context — RendererHasTextures (Hexa.NET.ImGui 2.2+)
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(ViewportSize.x, ViewportSize.y);
        io.ConfigFlags  |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;
        io.Fonts.RendererHasTextures = true;
    }

    public override void OnRHI_End()
    {
        if (_gl == null) return;
        _gl.DeleteProgram(_imProgram);
        _gl.DeleteBuffer(_imVbo);
        _gl.DeleteBuffer(_imEbo);
        _gl.DeleteVertexArray(_imVao);
        if (_fontTex  != 0) _gl.DeleteTexture(_fontTex);
        if (_whiteTex != 0) _gl.DeleteTexture(_whiteTex);
        foreach (var id in _texCache.Values) _gl.DeleteTexture(id);
        _texCache.Clear();
        foreach (var p in _imTexAllocs)
            System.Runtime.InteropServices.Marshal.FreeHGlobal(p);
        _imTexAllocs.Clear();
        ImGui.DestroyContext();
    }

    public override unsafe void OnRHI_FrameBegin(double dt)
    {
        if (_gl == null) return;

        uint w = (uint)Math.Max(1, ViewportSize.x);
        uint h = (uint)Math.Max(1, ViewportSize.y);
        _gl.Viewport(0, 0, w, h);
        _gl.ClearColor(0.12f, 0.12f, 0.13f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(ViewportSize.x, ViewportSize.y);
        io.DeltaTime   = (float)Math.Max(dt, 1e-6);
        io.AddMousePosEvent(_mousePos.X, _mousePos.Y);
        io.AddMouseButtonEvent(0, _mouseDown[0]);
        io.AddMouseButtonEvent(1, _mouseDown[1]);
        io.AddMouseButtonEvent(2, _mouseDown[2]);
        io.AddMouseWheelEvent(0f, _mouseScroll);
        _mouseScroll = 0;
        foreach (var ch in _inputChars) io.AddInputCharacter(ch);
        _inputChars.Clear();

        ImGui.NewFrame();
        Textures_Sync();

        // Fullscreen transparent host window — all UI components draw inside this
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(ViewportSize.x, ViewportSize.y));
        bool _uiOpen = true;
        ImGui.Begin("##ui_host", ref _uiOpen,
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoNav);
    }

    public override void OnRHI_FrameEnd(double dt)
    {
        ImGui.End(); // close ##ui_host
        ImGui.Render();
        ImGui_RenderDrawData(ImGui.GetDrawData());
    }

    public override unsafe void Draw2D_Image(A_Texture2D? tex, Vector2 pos, Vector2 size, Color tint)
    {
        if (tex == null || _gl == null) return;
        uint glTex = Texture_Get(tex);
        if (glTex == 0) return;

        // Find or create an ImTextureRef for this GL texture
        var imRef = ImTexRef_GetOrCreate(glTex);
        var dl    = ImGui.GetWindowDrawList();
        dl.AddImage(imRef, pos, pos + size, Vector2.Zero, Vector2.One, ToImColor(tint));
    }

    // Cache: GL texture handle → ImTextureRef (via heap-allocated ImTextureData)
    private readonly Dictionary<uint, ImTextureRef> _imTexRefs = new();
    private readonly List<nint> _imTexAllocs = new(); // for cleanup on RHI_End

    private unsafe ImTextureRef ImTexRef_GetOrCreate(uint glTex)
    {
        if (_imTexRefs.TryGetValue(glTex, out var cached)) return cached;

        // Heap-allocate an ImTextureData and initialise it with our GL handle.
        // ImGui only reads TexID when rendering draw-list commands, so setting
        // Status=Ok and filling TexID is sufficient.
        ImTextureData* p = (ImTextureData*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(ImTextureData));
        *p = default;
        ImTextureDataPtr dp = new ImTextureDataPtr(p);
        dp.SetTexID(new ImTextureID((ulong)glTex));
        dp.SetStatus(ImTextureStatus.Ok);

        var imRef = new ImTextureRef(dp);
        _imTexRefs[glTex] = imRef;
        _imTexAllocs.Add((nint)p);
        return imRef;
    }

    public override nint Texture_ImGuiID(A_Texture2D? tex)
    {
        if (tex == null) return 0;
        uint id = Texture_Get(tex);
        return (nint)id;
    }

    // Iterate ImGui's TexList and upload/destroy GL textures (ImGui 1.91+ managed texture system)
    private unsafe void Textures_Sync()
    {
        if (_gl == null) return;
        var fonts = ImGui.GetIO().Fonts;
        int count = fonts.TexList.Size;
        for (int i = 0; i < count; i++)
        {
            var td = fonts.TexList[i];
            if (td.IsNull) continue;

            if (td.Status == ImTextureStatus.WantCreate && td.Pixels != null)
            {
                uint tex = Tex_Upload(td);
                td.SetTexID(new ImTextureID((ulong)tex));
                td.SetStatus(ImTextureStatus.Ok);
                _fontTex = tex;
            }
            else if (td.Status == ImTextureStatus.WantUpdates && td.Pixels != null)
            {
                // Glyphs were lazily packed into the atlas — re-upload the whole texture
                uint texId = (uint)td.TexID.Handle;
                if (texId != 0)
                {
                    _gl.BindTexture(TextureTarget.Texture2D, texId);
                    _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                    Tex_UploadPixels(td);
                    td.SetStatus(ImTextureStatus.Ok);
                }
            }
            else if (td.Status == ImTextureStatus.WantDestroy)
            {
                uint texId = (uint)td.TexID.Handle;
                if (texId != 0) _gl.DeleteTexture(texId);
                td.SetStatus(ImTextureStatus.Destroyed);
                if (texId == _fontTex) _fontTex = 0;
            }
        }
    }

    private unsafe uint Tex_Upload(ImTextureDataPtr td)
    {
        uint tex = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        Tex_UploadPixels(td);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    private unsafe void Tex_UploadPixels(ImTextureDataPtr td)
    {
        if (td.BytesPerPixel == 1)
        {
            _gl!.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8,
                (uint)td.Width, (uint)td.Height, 0,
                PixelFormat.Red, PixelType.UnsignedByte, td.Pixels);
            int[] sw = [(int)GLEnum.One, (int)GLEnum.One, (int)GLEnum.One, (int)GLEnum.Red];
            _gl.TexParameterI(TextureTarget.Texture2D,
                (TextureParameterName)GLEnum.TextureSwizzleRgba, (ReadOnlySpan<int>)sw);
        }
        else
        {
            _gl!.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)td.Width, (uint)td.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, td.Pixels);
        }
    }

    // ================================================================================================================
    // 2D  — background draw list
    // ================================================================================================================


    // ================================================================================================================
    // IMGUI DRAW DATA RENDERER
    // ================================================================================================================

    private unsafe void ImGui_RenderDrawData(ImDrawDataPtr drawData)
    {
        if (_gl == null || drawData.CmdListsCount == 0) return;

        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        float[] proj =
        [
            2f/(R-L),      0,             0, 0,
            0,            -2f/(B-T),      0, 0,
            0,             0,            -1, 0,
            (R+L)/(L-R),  (T+B)/(B-T),   0, 1,
        ];

        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                              BlendingFactor.One,       BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.StencilTest);
        _gl.Enable(EnableCap.ScissorTest);

        _gl.UseProgram(_imProgram);
        _gl.Uniform1(_uImTex, 0);
        fixed (float* p = proj) _gl.UniformMatrix4(_uImProj, 1, false, p);

        _gl.BindVertexArray(_imVao);
        var clipOff = drawData.DisplayPos;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _imVbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert)),
                cmdList.VtxBuffer.Data, BufferUsageARB.StreamDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _imEbo);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort)),
                cmdList.IdxBuffer.Data, BufferUsageARB.StreamDraw);

            for (int ci = 0; ci < cmdList.CmdBuffer.Size; ci++)
            {
                var cmd = cmdList.CmdBuffer[ci];

                var clipMin = new Vector2(cmd.ClipRect.X - clipOff.X, cmd.ClipRect.Y - clipOff.Y);
                var clipMax = new Vector2(cmd.ClipRect.Z - clipOff.X, cmd.ClipRect.W - clipOff.Y);
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) continue;

                _gl.Scissor((int)clipMin.X,
                            ViewportSize.y - (int)clipMax.Y,
                            (uint)(clipMax.X - clipMin.X),
                            (uint)(clipMax.Y - clipMin.Y));

                uint texId = (uint)cmd.GetTexID().Handle;
                if (texId == 0) texId = _whiteTex;
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, texId);

                _gl.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    cmd.ElemCount,
                    DrawElementsType.UnsignedShort,
                    (void*)(cmd.IdxOffset * sizeof(ushort)),
                    (int)cmd.VtxOffset);
            }
        }

        _gl.Disable(EnableCap.ScissorTest);
    }

    // ================================================================================================================
    // HELPERS
    // ================================================================================================================

    private static uint ToImColor(Color c) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f));

    private uint Texture_Get(A_Texture2D texture)
    {
        if (_texCache.TryGetValue(texture, out uint id)) return id;
        var m = texture.ModelTexture;
        if (m.pixels == null || m.width <= 0 || m.height <= 0) return 0;

        id = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, id);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        unsafe
        {
            fixed (byte* p = m.pixels)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)m.width, (uint)m.height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,     (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,     (int)GLEnum.ClampToEdge);
        _texCache[texture] = id;
        return id;
    }

    private uint Program_Build(string vertSrc, string fragSrc)
    {
        uint vs = Shader_Compile(ShaderType.VertexShader,   vertSrc);
        uint fs = Shader_Compile(ShaderType.FragmentShader, fragSrc);
        uint prog = _gl!.CreateProgram();
        _gl.AttachShader(prog, vs); _gl.AttachShader(prog, fs);
        _gl.LinkProgram(prog);
        _gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) Console.WriteLine($"[RHI_OpenGL] link: {_gl.GetProgramInfoLog(prog)}");
        _gl.DetachShader(prog, vs); _gl.DetachShader(prog, fs);
        _gl.DeleteShader(vs); _gl.DeleteShader(fs);
        return prog;
    }

    private uint Shader_Compile(ShaderType type, string src)
    {
        uint s = _gl!.CreateShader(type);
        _gl.ShaderSource(s, src);
        _gl.CompileShader(s);
        _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) Console.WriteLine($"[RHI_OpenGL] {type}: {_gl.GetShaderInfoLog(s)}");
        return s;
    }

    private static string FontPath =>
        Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotJamMono", "Not Jam Mono Clean 16.ttf");
}
