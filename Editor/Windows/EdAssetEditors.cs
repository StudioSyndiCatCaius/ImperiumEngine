using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Windows;

public enum EMaterialPreviewMesh
{
    Sphere,
    Cube,
    Plane,
}

public class EdAssetEditor_Texture : EdAssetEditor
{
    public override void Rebuild()
    {
        EdAssetPreview_Texture preview = new()
        {
            texture = asset as A_Texture,
        };
        Layout_Split(preview);
    }
}

public class EdAssetEditor_Mesh : EdAssetEditor
{
    public override void Rebuild()
    {
        EdAssetPreview_3D preview = new();
        Layout_Split(preview);
        if (asset is A_Mesh mesh)
        {
            preview.BindMesh(mesh);
        }
    }
}

public class EdAssetEditor_Material : EdAssetEditor
{
    EdAssetPreview_3D _preview = null!;
    EMaterialPreviewMesh _shape = EMaterialPreviewMesh.Sphere;

    public override void Rebuild()
    {
        _preview = new EdAssetPreview_3D();

        C2_List extra = new()
        {
            orentation = EUIOrentation.V,
            layout = TLayout2.FULL,
        };
        C2_List bar = new()
        {
            orentation = EUIOrentation.H,
            spacing = 4,
            layout = new TLayout2
            {
                size = new Vector2(0, 28),
                size_min = new Vector2(0, 28),
                orient_H = EUIViewportAlignment.Fill,
            },
        };
        C2_EnumOption opt = new()
        {
            base_enum = typeof(EMaterialPreviewMesh),
            selected_enum = _shape,
            show_name = true,
            show_icon = true,
            orentation = EUIOrentation.H,
            on_change = e =>
            {
                _shape = (EMaterialPreviewMesh)e;
                if (asset is A_Material mat)
                {
                    _preview.BindMaterial(mat, _shape);
                }
            },
        };
        bar.Child_Add(opt);
        extra.Child_Add(bar);
        extra.Child_Add(_preview);
        Layout_Split(extra);
        if (asset is A_Material mat)
        {
            _preview.BindMaterial(mat, _shape);
        }
    }
}

[ImpClass(Hidden = true)]
public class EdAssetPreview_Texture : Imp2D
{
    public A_Texture? texture;

    static A_Texture? _checker;
    static Shader _hsb;
    static int _loc_hue;
    static int _loc_sat;
    static int _loc_bright;
    static bool _shader_tried;
    static bool _shader_ok;

    Texture2D _converted;
    uint _src_id;
    int _src_w;
    int _src_h;
    PixelFormat _src_format;
    A_Texture _draw = new();

    public EdAssetPreview_Texture()
    {
        cursor_filter = ECursorFilter.Hit;
        clip_children = true;
        layout = TLayout2.FULL;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Convert_Sync();
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X < 1f || dim.size.Y < 1f)
        {
            return;
        }

        Imp2D.Clip_Push(dim);
        Checker_Ensure();
        if (_checker != null)
        {
            TImage board = new()
            {
                texture = _checker,
                layout = EImageLayout.Tile,
                tiling = 1f,
            };
            board.Draw(dim);
        }

        Convert_Sync();
        Texture2D gpu = Gpu_Get();
        if (gpu.Id == 0)
        {
            Imp2D.Clip_Pop();
            return;
        }

        _draw.texture = gpu;
        EImageLayout fit = EImageLayout.Retain_Fit;
        if (texture != null)
        {
            fit = texture.ui_layout;
        }
        TImage img = new()
        {
            texture = _draw,
            layout = fit,
            clip_to_bounds = true,
        };

        A_Texture? src = texture;
        bool hsb = false;
        if (src != null && Shader_Ensure())
        {
            Raylib.SetShaderValue(_hsb, _loc_hue, src.hue, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_hsb, _loc_sat, src.saturation, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_hsb, _loc_bright, src.brightness, ShaderUniformDataType.Float);
            Raylib.BeginShaderMode(_hsb);
            hsb = true;
        }
        img.Draw(dim);
        if (hsb)
        {
            Raylib.EndShaderMode();
        }
        Imp2D.Clip_Pop();
    }

    protected override void OnDestroy()
    {
        Convert_Clear();
        base.OnDestroy();
    }

    Texture2D Gpu_Get()
    {
        if (_converted.Id != 0)
        {
            return _converted;
        }
        if (texture != null)
        {
            return texture.texture;
        }
        return default;
    }

    void Convert_Sync()
    {
        if (texture == null)
        {
            Convert_Clear();
            _src_id = 0;
            return;
        }

        Texture2D src = texture.texture;
        PixelFormat want = texture.pixel_format;
        if (src.Id == _src_id && src.Width == _src_w && src.Height == _src_h && want == _src_format)
        {
            return;
        }

        Convert_Clear();
        _src_id = src.Id;
        _src_w = src.Width;
        _src_h = src.Height;
        _src_format = want;

        if (src.Id == 0)
        {
            return;
        }
        if (want == 0 || want == src.Format)
        {
            return;
        }

        Image img = Raylib.LoadImageFromTexture(src);
        if (img.Width <= 0 || img.Height <= 0)
        {
            return;
        }
        Raylib.ImageFormat(ref img, want);
        if (img.Width > 0 && img.Height > 0)
        {
            _converted = Raylib.LoadTextureFromImage(img);
        }
        Raylib.UnloadImage(img);
    }

    void Convert_Clear()
    {
        if (_converted.Id != 0)
        {
            Raylib.UnloadTexture(_converted);
            _converted = default;
        }
    }

    static void Checker_Ensure()
    {
        if (_checker != null)
        {
            return;
        }
        Image img = Raylib.GenImageChecked(32, 32, 2, 2,
            new Color(42, 42, 42, 255),
            new Color(58, 58, 58, 255));
        Texture2D gpu = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        _checker = new A_Texture
        {
            texture = gpu,
        };
    }

    static bool Shader_Ensure()
    {
        if (_shader_tried)
        {
            return _shader_ok;
        }
        _shader_tried = true;
        _hsb = Raylib.LoadShaderFromMemory(null, HSB_FS);
        if (!Raylib.IsShaderValid(_hsb))
        {
            _shader_ok = false;
            return false;
        }
        _loc_hue = Raylib.GetShaderLocation(_hsb, "u_hue");
        _loc_sat = Raylib.GetShaderLocation(_hsb, "u_sat");
        _loc_bright = Raylib.GetShaderLocation(_hsb, "u_bright");
        _shader_ok = true;
        return true;
    }

    const string HSB_FS = """
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform float u_hue;
uniform float u_sat;
uniform float u_bright;
out vec4 finalColor;

vec3 rgb2hsv(vec3 c)
{
    vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
    vec3 p = abs(fract(c.xxx + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void main()
{
    vec4 t = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
    vec3 hsv = rgb2hsv(t.rgb);
    hsv.x = fract(hsv.x + u_hue / 360.0);
    hsv.y = clamp(hsv.y * u_sat, 0.0, 1.0);
    hsv.z = hsv.z * u_bright;
    finalColor = vec4(hsv2rgb(hsv), t.a);
}
""";
}

[ImpClass(Hidden = true)]
public class EdAssetPreview_3D : Imp2D
{
    public C2_Viewport3D viewport = new();

    ImpScene _scene;
    C3_Mesh _mesh;

    enum EDrag { None, Orbit, Pan }
    EDrag _drag;
    float _look_dist = 4f;

    public EdAssetPreview_3D()
    {
        cursor_filter = ECursorFilter.Hit;
        layout = TLayout2.FULL;

        _scene = new ImpScene();
        _scene.root = new Imp3D { name = "Preview" };
        _mesh = new C3_Mesh
        {
            name = "PreviewMesh",
            physics_enabled = false,
            cast_shadows = true,
        };
        _scene.root.Child_Add(_mesh);

        viewport.view_scene = _scene;
        viewport.root = _scene.root;
        viewport.cursor_filter = ECursorFilter.Pass;
        viewport.layout = TLayout2.FULL;
        viewport.draw_flags = EDrawFlags.None;
        Child_Add(viewport);
        Frame();
    }

    public void BindMesh(A_Mesh mesh)
    {
        _mesh.mesh = mesh;
        _mesh.materials.Clear();
        Frame();
    }

    public void BindMaterial(A_Material mat, EMaterialPreviewMesh shape)
    {
        A_Mesh geo = A_Mesh.GEO_SPHERE;
        if (shape == EMaterialPreviewMesh.Cube)
        {
            geo = A_Mesh.GEO_CUBE;
        }
        else if (shape == EMaterialPreviewMesh.Plane)
        {
            geo = A_Mesh.GEO_PLANE;
        }
        _mesh.mesh = geo;
        _mesh.materials.Clear();
        if (mat != null)
        {
            _mesh.materials.Add(mat);
        }
        Frame();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        viewport.view_scene = _scene;
        viewport.root = _scene.root;
        Camera_Update(dt);
    }

    protected override void OnDestroy()
    {
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer player = ImpPlayer.players[0];
            Drag_End(player);
        }
        if (_scene != null && _scene.root != null)
        {
            _scene.root.Destroy();
        }
        base.OnDestroy();
    }

    void Frame()
    {
        TBounds3 b = TBounds3.ZERO;
        if (_mesh.mesh != null)
        {
            b = _mesh.mesh.Bounds_Get(new TTransform3());
        }
        Vector3 center = b.center;
        float radius = 0.5f;
        if (!b.IsEmpty)
        {
            radius = MathF.Max(0.25f, b.size.Length() * 0.5f);
        }
        float half_fov = viewport.camera.FovY * 0.5f * (MathF.PI / 180f);
        float sin = MathF.Max(0.05f, MathF.Sin(half_fov));
        _look_dist = Math.Clamp(radius / sin * 1.35f, 0.5f, 10000f);
        Vector3 fwd = Vector3.Normalize(new Vector3(1f, 0.55f, 1f));
        viewport.camera.Target = center;
        viewport.camera.Position = center + fwd * _look_dist;
    }

    void Camera_Update(double dt)
    {
        if (!IsVisibleInTree() || ImpPlayer.players.Count == 0)
        {
            return;
        }
        ImpPlayer player = ImpPlayer.players[0];
        if (player.target_focus is C2_TextEdit te && te.is_focused && ImpPlayer.Target_IsLive(te))
        {
            return;
        }

        TDimensions2 dim = viewport.Dimensions_Get();
        bool over = player.Cursor_IsInDimensions(dim);
        bool lmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool mmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Middle);
        bool lmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left);
        bool mmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Middle);
        bool ui_block = player.target_cursor is C2_Seperator
            || player.input_hog is C2_Seperator
            || player.grab_is_active;

        if (over && !ui_block && (lmb_p || mmb_p))
        {
            player.target_focus = this;
        }

        bool ours = player.target_focus == this;
        if (!over && !ours && _drag == EDrag.None)
        {
            return;
        }

        bool hogged = player.input_hog != null && player.input_hog != this;
        if (hogged && _drag == EDrag.None)
        {
            return;
        }

        if (_drag != EDrag.None)
        {
            if ((_drag == EDrag.Orbit && !lmb) || (_drag == EDrag.Pan && !mmb))
            {
                Drag_End(player);
            }
            else
            {
                Camera_Drag(Raylib.GetMouseDelta());
            }
            return;
        }

        if (over && ours && !hogged && !ui_block)
        {
            if (lmb_p)
            {
                Drag_Begin(player, EDrag.Orbit);
            }
            else if (mmb_p)
            {
                Drag_Begin(player, EDrag.Pan);
            }
        }

        if (_drag == EDrag.None && over && !hogged && !ui_block)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                Vector3 fwd = CamFwd();
                _look_dist = Math.Clamp(_look_dist * MathF.Pow(0.85f, wheel), 0.2f, 10000f);
                viewport.camera.Position = viewport.camera.Target - fwd * _look_dist;
            }
        }
    }

    void Camera_Drag(Vector2 md)
    {
        Vector3 fwd = CamFwd();
        Vector3 right = CamRight(fwd);
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, fwd));
        const float look_sens = 0.0045f;
        const float pan_sens = 0.0018f;

        if (_drag == EDrag.Orbit)
        {
            float yaw = MathF.Atan2(fwd.X, fwd.Z) - md.X * look_sens;
            float pitch = MathF.Asin(Math.Clamp(fwd.Y, -1f, 1f)) - md.Y * look_sens;
            pitch = Math.Clamp(pitch, -1.53f, 1.53f);
            fwd = new Vector3(
                MathF.Cos(pitch) * MathF.Sin(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Cos(yaw));
            viewport.camera.Position = viewport.camera.Target - fwd * _look_dist;
        }
        else if (_drag == EDrag.Pan)
        {
            Vector3 delta = (-right * md.X + up * md.Y) * _look_dist * pan_sens;
            viewport.camera.Position += delta;
            viewport.camera.Target += delta;
        }
    }

    void Drag_Begin(ImpPlayer player, EDrag mode)
    {
        _drag = mode;
        player.target_focus = this;
        player.input_hog = this;
    }

    void Drag_End(ImpPlayer player)
    {
        _drag = EDrag.None;
        if (player != null && player.input_hog == this)
        {
            player.input_hog = null;
        }
    }

    Vector3 CamFwd()
    {
        Vector3 fwd = viewport.camera.Target - viewport.camera.Position;
        if (fwd.LengthSquared() < 1e-8f)
        {
            return -Vector3.UnitZ;
        }
        return Vector3.Normalize(fwd);
    }

    static Vector3 CamRight(Vector3 fwd)
    {
        Vector3 right = Vector3.Cross(fwd, Vector3.UnitY);
        if (right.LengthSquared() < 1e-8f)
        {
            return Vector3.UnitX;
        }
        return Vector3.Normalize(right);
    }
}
