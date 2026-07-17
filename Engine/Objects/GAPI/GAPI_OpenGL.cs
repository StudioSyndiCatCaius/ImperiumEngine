using System.Drawing;
using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Structs;
using ImperiumEngine.Classes;
using Silk.NET.OpenGL;
using StbTrueTypeSharp;

namespace ImperiumEngine.Renderers;

// OpenGL 3.3 backend. 2D pixel space: origin top-left, +x right, +y down.
public class RHI_OpenGL : ImpRender
{
    private GL? _gl;
    private readonly float[] _proj = new float[16];

    // solid colored-quad pipeline
    private uint _program, _vao, _vbo;
    private int _uProj, _uColor;

    // rounded-rect pipeline (reuses _vao/_vbo geometry, SDF in the fragment shader)
    private uint _roundProgram;
    private int _uRoundProj, _uRoundColor, _uRoundCenter, _uRoundHalf, _uRoundRadius;

    // text pipeline
    private uint _textProgram, _textVao, _textVbo, _fontTex;
    private int _uTextProj, _uTextColor, _uTextTex;

    // textured-image pipeline (reuses the text VAO/VBO: pos + uv)
    private uint _imgProgram;
    private int _uImgProj, _uImgColor, _uImgTex;
    private readonly Dictionary<A_Texture2D, uint> _texCache = new();

    // pixel-blit pipeline: one PBO for async upload, pooled textures by (w,h) to avoid GPU realloc
    private uint _pixelPbo;
    private readonly Dictionary<(int, int), uint> _pixelTexPool = new();

    // 3D mesh pipeline
    private uint _meshProgram, _meshVao, _meshVbo, _meshEbo;
    private int _uMeshModel, _uMeshView, _uMeshProj;
    private Matrix4x4 _view3D = Matrix4x4.Identity;
    private Matrix4x4 _proj3D = Matrix4x4.Identity;

    // render target FBOs keyed by asset reference
    private struct RtEntry { public uint fbo, colorTex, depthRbo; public int w, h; }
    private readonly Dictionary<A_RenderTarget, RtEntry> _rtCache = new();

    private const int BAKE = 16;          // atlas is baked at this pixel height
    private Glyph[]? _font;               // ASCII 32..126
    private float _ascent, _lineHeight;

    private struct Glyph
    {
        public float u0, v0, u1, v1;
        public float xoff, yoff;
        public int w, h;
        public float advance;
    }

    private const string VertSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        uniform mat4 uProj;
        void main() { gl_Position = uProj * vec4(aPos, 0.0, 1.0); }
        """;

    private const string FragSrc = """
        #version 330 core
        out vec4 FragColor;
        uniform vec4 uColor;
        void main() { FragColor = uColor; }
        """;

    private const string RoundVertSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        uniform mat4 uProj;
        out vec2 vWorld;
        void main() { vWorld = aPos; gl_Position = uProj * vec4(aPos, 0.0, 1.0); }
        """;

    private const string RoundFragSrc = """
        #version 330 core
        in vec2 vWorld;
        out vec4 FragColor;
        uniform vec4 uColor;
        uniform vec2 uCenter;
        uniform vec2 uHalf;
        uniform float uRadius;
        float sdRoundBox(vec2 p, vec2 b, float r) {
            vec2 q = abs(p) - b + r;
            return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
        }
        void main() {
            float d = sdRoundBox(vWorld - uCenter, uHalf, uRadius);
            float a = 1.0 - smoothstep(-0.75, 0.75, d);
            if (a <= 0.0) discard;
            FragColor = vec4(uColor.rgb, uColor.a * a);
        }
        """;

    private const string TextVertSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUV;
        uniform mat4 uProj;
        out vec2 vUV;
        void main() { gl_Position = uProj * vec4(aPos, 0.0, 1.0); vUV = aUV; }
        """;

    private const string TextFragSrc = """
        #version 330 core
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D uTex;
        uniform vec4 uColor;
        void main() { FragColor = vec4(uColor.rgb, uColor.a * texture(uTex, vUV).r); }
        """;

    // RGBA image tinted by uColor (shares TextVertSrc: pos + uv)
    private const string ImgFragSrc = """
        #version 330 core
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D uTex;
        uniform vec4 uColor;
        void main() { FragColor = texture(uTex, vUV) * uColor; }
        """;

    // 3D mesh pipeline: interleaved pos+normal, MVP
    private const string Mesh3DVertSrc =
        "#version 330 core\n" +
        "layout(location=0) in vec3 aPos;\n" +
        "layout(location=1) in vec3 aNorm;\n" +
        "uniform mat4 uModel;\n" +
        "uniform mat4 uView;\n" +
        "uniform mat4 uProj;\n" +
        "out vec3 vNormal;\n" +
        "void main() {\n" +
        "    vNormal = mat3(uModel) * aNorm;\n" +
        "    gl_Position = uProj * uView * uModel * vec4(aPos, 1.0);\n" +
        "}\n";

    // zero-uniform face-tint shader: X=red, Y=green, Z=blue faces — good for debug
    private const string Mesh3DFragSrc =
        "#version 330 core\n" +
        "in vec3 vNormal;\n" +
        "out vec4 FragColor;\n" +
        "void main() {\n" +
        "    vec3 c = abs(normalize(vNormal)) * 0.75 + 0.25;\n" +
        "    FragColor = vec4(c, 1.0);\n" +
        "}\n";

    public override void OnRHI_Init()
    {
        var window = App?.Window;
        if (window == null) return;

        _gl = window.CreateOpenGL();
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // solid pipeline
        _program = Program_Build(VertSrc, FragSrc);
        _uProj = _gl.GetUniformLocation(_program, "uProj");
        _uColor = _gl.GetUniformLocation(_program, "uColor");
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.EnableVertexAttribArray(0);
        unsafe { _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0); }

        // rounded-rect pipeline (shares the solid quad geometry)
        _roundProgram = Program_Build(RoundVertSrc, RoundFragSrc);
        _uRoundProj = _gl.GetUniformLocation(_roundProgram, "uProj");
        _uRoundColor = _gl.GetUniformLocation(_roundProgram, "uColor");
        _uRoundCenter = _gl.GetUniformLocation(_roundProgram, "uCenter");
        _uRoundHalf = _gl.GetUniformLocation(_roundProgram, "uHalf");
        _uRoundRadius = _gl.GetUniformLocation(_roundProgram, "uRadius");

        // text pipeline
        _textProgram = Program_Build(TextVertSrc, TextFragSrc);
        _uTextProj = _gl.GetUniformLocation(_textProgram, "uProj");
        _uTextColor = _gl.GetUniformLocation(_textProgram, "uColor");
        _uTextTex = _gl.GetUniformLocation(_textProgram, "uTex");
        _textVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_textVao);
        _textVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        }
        _gl.BindVertexArray(0);

        // textured-image pipeline (shares the text vertex layout + VAO/VBO)
        _imgProgram = Program_Build(TextVertSrc, ImgFragSrc);
        _uImgProj = _gl.GetUniformLocation(_imgProgram, "uProj");
        _uImgColor = _gl.GetUniformLocation(_imgProgram, "uColor");
        _uImgTex = _gl.GetUniformLocation(_imgProgram, "uTex");

        _pixelPbo = _gl.GenBuffer();

        Font_Load(FontPath);

        // 3D mesh pipeline
        _meshProgram = Program_Build(Mesh3DVertSrc, Mesh3DFragSrc);
        _uMeshModel = _gl.GetUniformLocation(_meshProgram, "uModel");
        _uMeshView  = _gl.GetUniformLocation(_meshProgram, "uView");
        _uMeshProj  = _gl.GetUniformLocation(_meshProgram, "uProj");

        _meshVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_meshVao);
        _meshVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _meshVbo);
        _meshEbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _meshEbo);

        const uint stride = 6 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);
        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        }
        _gl.BindVertexArray(0);
    }

    public override void OnRHI_End()
    {
        if (_gl == null) return;
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_program);
        _gl.DeleteProgram(_roundProgram);
        _gl.DeleteBuffer(_textVbo);
        _gl.DeleteVertexArray(_textVao);
        _gl.DeleteProgram(_textProgram);
        _gl.DeleteProgram(_imgProgram);
        if (_fontTex != 0) _gl.DeleteTexture(_fontTex);
        foreach (var id in _texCache.Values) _gl.DeleteTexture(id);
        _texCache.Clear();
        _gl.DeleteBuffer(_pixelPbo);
        foreach (var id in _pixelTexPool.Values) _gl.DeleteTexture(id);
        _pixelTexPool.Clear();

        _gl.DeleteBuffer(_meshVbo);
        _gl.DeleteBuffer(_meshEbo);
        _gl.DeleteVertexArray(_meshVao);
        _gl.DeleteProgram(_meshProgram);
        foreach (var e in _rtCache.Values)
        {
            _gl.DeleteFramebuffer(e.fbo);
            _gl.DeleteTexture(e.colorTex);
            _gl.DeleteRenderbuffer(e.depthRbo);
        }
        _rtCache.Clear();
    }

    public override void OnRHI_FrameBegin(double dt)
    {
        if (_gl == null) return;

        uint w = (uint)Math.Max(1, ViewportSize.x);
        uint h = (uint)Math.Max(1, ViewportSize.y);
        _gl.Viewport(0, 0, w, h);
        _gl.ClearColor(0.12f, 0.12f, 0.13f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        Ortho_TopLeft(ViewportSize.x, ViewportSize.y, _proj);
    }

    // ================================================================================================================
    // 2D
    // ================================================================================================================
    // Upload an RGBA8 pixel buffer and blit it as a textured quad.
    // Fast path: pooled GL texture (no GPU realloc) + PBO orphaning (no CPU/GPU sync stall).
    public override void Draw2D_pixels(TDraw2DData data)
    {
        if (_gl == null || data.pixels == null || data.size.x <= 0 || data.size.y <= 0) return;

        int w = data.size.x, h = data.size.y;
        nuint byteCount = (nuint)(w * h * 4);

        uint tex = Pixel_GetTex(w, h);

        // Orphan the PBO (new backing store, no GPU sync), upload pixels, then let TexSubImage2D
        // read from the PBO asynchronously via DMA instead of blocking the command queue.
        _gl.BindBuffer(BufferTargetARB.PixelUnpackBuffer, _pixelPbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.PixelUnpackBuffer, byteCount, (void*)0, BufferUsageARB.StreamDraw);
            fixed (byte* p = data.pixels)
                _gl.BufferSubData(BufferTargetARB.PixelUnpackBuffer, 0, byteCount, p);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)w, (uint)h,
                PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
        }
        _gl.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);

        float x = data.pos.x, y = data.pos.y, xr = x + w, yb = y + h;
        Span<float> verts =
        [
            x,  y,  0, 0,   xr, y,  1, 0,   xr, yb, 1, 1,
            x,  y,  0, 0,   xr, yb, 1, 1,   x,  yb, 0, 1,
        ];

        _gl.UseProgram(_imgProgram);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uImgProj, 1, false, p); }
        _gl.Uniform4(_uImgColor, 1f, 1f, 1f, 1f);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.Uniform1(_uImgTex, 0);

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        unsafe
        {
            fixed (float* v = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    // Allocate a GPU texture once per unique (w,h); subsequent calls return the cached id.
    private uint Pixel_GetTex(int w, int h)
    {
        var key = (w, h);
        if (_pixelTexPool.TryGetValue(key, out uint id)) return id;

        id = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, id);
        unsafe { _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0); }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _pixelTexPool[key] = id;
        return id;
    }

    public override void Draw2D_Rect(TVector2i pos, TVector2i size, Color color)
    {
        if (_gl == null) return;
        Quad_Fill(pos.x, pos.y, size.x, size.y, color);
    }

    public override void Draw2D_RectRounded(TVector2i pos, TVector2i size, Color color, float radius)
    {
        if (_gl == null || size.x <= 0 || size.y <= 0) return;

        float r = MathF.Min(radius, MathF.Min(size.x, size.y) * 0.5f);
        if (r <= 0.5f) { Quad_Fill(pos.x, pos.y, size.x, size.y, color); return; }

        float x = pos.x, y = pos.y, w = size.x, h = size.y, xr = x + w, yb = y + h;
        Span<float> verts = [ x, y, xr, y, xr, yb,  x, y, xr, yb, x, yb ];

        _gl.UseProgram(_roundProgram);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uRoundProj, 1, false, p); }
        _gl.Uniform4(_uRoundColor, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        _gl.Uniform2(_uRoundCenter, x + w * 0.5f, y + h * 0.5f);
        _gl.Uniform2(_uRoundHalf, w * 0.5f, h * 0.5f);
        _gl.Uniform1(_uRoundRadius, r);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* v = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    public override void Draw2D_Texture(A_Texture2D texture, TVector2i pos, TVector2i size, Color tint)
    {
        if (_gl == null || texture == null) return;

        uint tex = Texture_Get(texture);
        if (tex == 0) return;

        float x = pos.x, y = pos.y, xr = x + size.x, yb = y + size.y;
        Span<float> verts =
        [
            x, y, 0, 0,  xr, y, 1, 0,  xr, yb, 1, 1,
            x, y, 0, 0,  xr, yb, 1, 1,  x, yb, 0, 1,
        ];

        _gl.UseProgram(_imgProgram);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uImgProj, 1, false, p); }
        _gl.Uniform4(_uImgColor, tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.Uniform1(_uImgTex, 0);

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        unsafe
        {
            fixed (float* v = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    // lazily upload an asset's pixels to a GL texture, cached by asset reference
    private uint Texture_Get(A_Texture2D texture)
    {
        if (_texCache.TryGetValue(texture, out uint id)) return id;
        if (texture.ModelTexture.pixels == null || texture.ModelTexture.width <= 0 || texture.ModelTexture.height <= 0) return 0;

        id = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, id);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        unsafe
        {
            fixed (byte* p = texture.ModelTexture.pixels)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)texture.ModelTexture.width, (uint)texture.ModelTexture.height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _texCache[texture] = id;
        return id;
    }
    

    public override bool Draw2D_Button(TVector2i pos, TVector2i size, string text, Color color)
    {
        if (_gl == null) return false;
        Quad_Fill(pos.x, pos.y, size.x, size.y, color);
        if (!string.IsNullOrEmpty(text))
        {
            var ts = Text_Measure(text, BAKE);
            var tp = new TVector2i(pos.x + (size.x - ts.x) / 2, pos.y + (size.y - ts.y) / 2);
            Draw2D_Text(tp, text, BAKE, Color.White);
        }
        // TODO: hover/press state once input exists
        return false;
    }

    public override void Draw2D_Text(TVector2i pos, string text, int size, Color color)
    {
        if (_gl == null || _font == null || string.IsNullOrEmpty(text)) return;

        float scale = size / (float)BAKE;
        float penX = pos.x;
        float baseline = pos.y + _ascent * scale;

        var verts = new List<float>(text.Length * 24);
        foreach (char ch in text)
        {
            if (ch < 32 || ch > 126)
            {
                if (ch == '\t') penX += _font[0].advance * 4 * scale;
                continue;
            }

            var g = _font[ch - 32];
            if (g.w > 0 && g.h > 0)
            {
                float x0 = penX + g.xoff * scale, y0 = baseline + g.yoff * scale;
                float x1 = x0 + g.w * scale, y1 = y0 + g.h * scale;
                verts.AddRange(new[]
                {
                    x0, y0, g.u0, g.v0,  x1, y0, g.u1, g.v0,  x1, y1, g.u1, g.v1,
                    x0, y0, g.u0, g.v0,  x1, y1, g.u1, g.v1,  x0, y1, g.u0, g.v1,
                });
            }
            penX += g.advance * scale;
        }
        if (verts.Count == 0) return;

        var arr = verts.ToArray();
        _gl.UseProgram(_textProgram);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uTextProj, 1, false, p); }
        _gl.Uniform4(_uTextColor, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _fontTex);
        _gl.Uniform1(_uTextTex, 0);

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        unsafe
        {
            fixed (float* v = arr)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(arr.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(arr.Length / 4));
    }

    public override TVector2i Text_Measure(string text, int size)
    {
        if (_font == null || string.IsNullOrEmpty(text)) return new TVector2i(0, 0);

        float scale = size / (float)BAKE;
        float w = 0;
        foreach (char ch in text)
        {
            if (ch < 32 || ch > 126)
            {
                if (ch == '\t') w += _font[0].advance * 4;
                continue;
            }
            w += _font[ch - 32].advance;
        }
        return new TVector2i((int)MathF.Ceiling(w * scale), (int)MathF.Ceiling(_lineHeight * scale));
    }

    // ================================================================================================================
    // RENDER TARGETS
    // ================================================================================================================

    public override void RT_Viewport_Begin(A_RenderTarget rt, int w, int h)
    {
        if (_gl == null || w <= 0 || h <= 0) return;
        var e = RT_GetOrCreate(rt, w, h);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, e.fbo);
        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.Enable(EnableCap.DepthTest);
        var bg = rt.backgroundColor;
        _gl.ClearColor(bg.R / 255f, bg.G / 255f, bg.B / 255f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public override void RT_Viewport_End(A_RenderTarget rt)
    {
        if (_gl == null) return;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Viewport(0, 0, (uint)Math.Max(1, ViewportSize.x), (uint)Math.Max(1, ViewportSize.y));
    }

    public override void Viewport_SetCamera(ImpCamera camera, int w, int h)
    {
        float near   = Math.Max(camera.NearPlane, 0.01f);
        float fovRad = MathF.Max(camera.FOV, 1f) * MathF.PI / 180f;
        float aspect = w / (float)Math.Max(1, h);
        _proj3D = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, near, camera.FarPlane);

        // guard against zero/uninitialized quaternion
        var rot = (camera.Rotation.LengthSquared() < 0.0001f) ? Quaternion.Identity : camera.Rotation;
        Vector3 fwd = Vector3.Transform(-Vector3.UnitZ, rot);
        Vector3 up  = Vector3.Transform( Vector3.UnitY, rot);
        _view3D = Matrix4x4.CreateLookAt(camera.Position, camera.Position + fwd, up);
    }

    public override void Draw2D_RenderTarget(A_RenderTarget rt, TVector2i pos, TVector2i size)
    {
        if (_gl == null || !_rtCache.TryGetValue(rt, out var e) || size.x <= 0 || size.y <= 0) return;

        // Y-flip UVs: FBO origin is bottom-left, screen origin is top-left
        float x = pos.x, y = pos.y, xr = x + size.x, yb = y + size.y;
        Span<float> verts =
        [
            x,  y,  0, 1,   xr, y,  1, 1,   xr, yb, 1, 0,
            x,  y,  0, 1,   xr, yb, 1, 0,    x, yb, 0, 0,
        ];

        _gl.UseProgram(_imgProgram);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uImgProj, 1, false, p); }
        _gl.Uniform4(_uImgColor, 1f, 1f, 1f, 1f);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, e.colorTex);
        _gl.Uniform1(_uImgTex, 0);

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        unsafe
        {
            fixed (float* v = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    // ================================================================================================================
    // 3D
    // ================================================================================================================

    public override bool Draw3D_MeshFast(TTransform3D transform, TModel_Mesh mesh)
    {
        if (_gl == null || mesh.vertices == null || mesh.indices == null) return false;

        var model = Matrix4x4.CreateScale(transform.scale)
                  * Matrix4x4.CreateFromQuaternion(transform.rotation == default ? Quaternion.Identity : transform.rotation)
                  * Matrix4x4.CreateTranslation(transform.position);

        _gl.UseProgram(_meshProgram);
        Mesh_Matrix(_uMeshModel, model);
        Mesh_Matrix(_uMeshView,  _view3D);
        Mesh_Matrix(_uMeshProj,  _proj3D);

        _gl.BindVertexArray(_meshVao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _meshVbo);
        unsafe
        {
            fixed (float* v = mesh.vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(mesh.vertices.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _meshEbo);
        unsafe
        {
            fixed (uint* i = mesh.indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                    (nuint)(mesh.indices.Length * sizeof(uint)), i, BufferUsageARB.DynamicDraw);
        }

        _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.indices.Length, DrawElementsType.UnsignedInt, 0);
        _gl.BindVertexArray(0);
        return true;
    }

    private unsafe void Mesh_Matrix(int loc, Matrix4x4 m)
    {
        // System.Numerics is row-major; GLSL expects column-major → transpose on upload
        _gl!.UniformMatrix4(loc, 1, true, (float*)&m);
    }

    private RtEntry RT_GetOrCreate(A_RenderTarget rt, int w, int h)
    {
        if (_rtCache.TryGetValue(rt, out var e) && e.w == w && e.h == h) return e;

        if (_rtCache.TryGetValue(rt, out var old))
        {
            _gl!.DeleteFramebuffer(old.fbo);
            _gl.DeleteTexture(old.colorTex);
            _gl.DeleteRenderbuffer(old.depthRbo);
            _rtCache.Remove(rt);
        }

        uint fbo = _gl!.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

        uint colorTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, colorTex);
        unsafe { _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0); }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorTex, 0);

        uint depthRbo = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)w, (uint)h);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRbo);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        e = new RtEntry { fbo = fbo, colorTex = colorTex, depthRbo = depthRbo, w = w, h = h };
        _rtCache[rt] = e;
        return e;
    }

    // ----------------------------------------------------------------------------------------------------------------

    private void Quad_Fill(float x, float y, float w, float h, Color color)
    {
        float r = x + w, b = y + h;
        Span<float> verts =
        [
            x, y,  r, y,  r, b,
            x, y,  r, b,  x, b,
        ];

        _gl!.UseProgram(_program);
        unsafe { fixed (float* p = _proj) _gl.UniformMatrix4(_uProj, 1, false, p); }
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* v = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        }
        _gl.Uniform4(_uColor, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    private static string FontPath =>
        Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotJamMono", "Not Jam Mono Clean 16.ttf");

    private unsafe void Font_Load(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"[RHI_OpenGL] font not found: {path}"); return; }

        var font = StbTrueType.CreateFont(File.ReadAllBytes(path), 0);
        if (font == null) { Console.WriteLine("[RHI_OpenGL] failed to parse font"); return; }

        const int AW = 256, AH = 256;
        var atlas = new byte[AW * AH];
        var glyphs = new Glyph[95];

        float scale = StbTrueType.stbtt_ScaleForPixelHeight(font, BAKE);
        int ascent, descent, lineGap;
        StbTrueType.stbtt_GetFontVMetrics(font, &ascent, &descent, &lineGap);
        _ascent = ascent * scale;
        _lineHeight = (ascent - descent + lineGap) * scale;

        int penX = 0, penY = 0, rowH = 0;
        for (int i = 0; i < 95; i++)
        {
            int cp = 32 + i;
            int adv, lsb;
            StbTrueType.stbtt_GetCodepointHMetrics(font, cp, &adv, &lsb);

            int x0, y0, x1, y1;
            StbTrueType.stbtt_GetCodepointBitmapBox(font, cp, scale, scale, &x0, &y0, &x1, &y1);
            int gw = x1 - x0, gh = y1 - y0;

            if (gw > 0 && gh > 0)
            {
                if (penX + gw >= AW) { penX = 0; penY += rowH + 1; rowH = 0; }

                var glyphBuf = new byte[gw * gh];
                fixed (byte* gp = glyphBuf)
                    StbTrueType.stbtt_MakeCodepointBitmap(font, gp, gw, gh, gw, scale, scale, cp);

                for (int row = 0; row < gh; row++)
                    for (int col = 0; col < gw; col++)
                        atlas[(penY + row) * AW + penX + col] = glyphBuf[row * gw + col];

                glyphs[i] = new Glyph
                {
                    u0 = penX / (float)AW, v0 = penY / (float)AH,
                    u1 = (penX + gw) / (float)AW, v1 = (penY + gh) / (float)AH,
                    xoff = x0, yoff = y0, w = gw, h = gh, advance = adv * scale,
                };

                penX += gw + 1;
                rowH = Math.Max(rowH, gh);
            }
            else
            {
                glyphs[i] = new Glyph { advance = adv * scale };
            }
        }

        _fontTex = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* a = atlas)
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, AW, AH, 0, PixelFormat.Red, PixelType.UnsignedByte, a);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _font = glyphs;
    }

    private uint Program_Build(string vertSrc, string fragSrc)
    {
        uint vs = Shader_Compile(ShaderType.VertexShader, vertSrc);
        uint fs = Shader_Compile(ShaderType.FragmentShader, fragSrc);

        uint program = _gl!.CreateProgram();
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) Console.WriteLine($"[RHI_OpenGL] program link failed: {_gl.GetProgramInfoLog(program)}");

        _gl.DetachShader(program, vs);
        _gl.DetachShader(program, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        return program;
    }

    private uint Shader_Compile(ShaderType type, string src)
    {
        uint shader = _gl!.CreateShader(type);
        _gl.ShaderSource(shader, src);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) Console.WriteLine($"[RHI_OpenGL] {type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    // column-major orthographic projection, pixel space with top-left origin
    private static void Ortho_TopLeft(int width, int height, float[] m)
    {
        float w = Math.Max(1, width), h = Math.Max(1, height);
        Array.Clear(m);
        m[0] = 2f / w;
        m[5] = -2f / h;
        m[10] = -1f;
        m[12] = -1f;
        m[13] = 1f;
        m[15] = 1f;
    }
}
