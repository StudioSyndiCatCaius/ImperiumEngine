using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

/// <summary>
/// Draws 3D comps from a scene and/or root. Optional transpose_traces remaps
/// screen picks through this widget's camera instead of the app camera.
/// </summary>
public class C2_Viewport3D : Imp2D
{
    public ImpScene? view_scene;
    public ImpComp? root;
    public ImpComp? overlay;
    public bool transpose_traces = true;
    // Editor scene view sets Editor (G toggle). Game / standalone leave None.
    public EDrawFlags draw_flags;
    // When set, this widget looks through a live C3_Camera (play starting_camera) instead of `camera`.
    public Imp3D? view_camera;

    public Camera3D camera = new()
    {
        Position = new Vector3(6.5f, 4.5f, 8.5f),
        Target = new Vector3(0f, 0.5f, 0f),
        Up = Vector3.UnitY,
        FovY = 50f,
        Projection = CameraProjection.Perspective,
    };

    RenderTexture2D target;
    int target_w;
    int target_h;

    public C2_Viewport3D()
    {
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;
        cursor_filter = ECursorFilter.Hit;
    }

    public ImpComp Root_Get()
    {
        if (root != null)
        {
            return root;
        }
        return view_scene?.root;
    }

    Camera3D Camera_GetRL()
    {
        if (view_camera != null && view_camera.Camera_IsValid())
        {
            return R3D.CameraToRL(view_camera.Camera_GetData());
        }
        return camera;
    }

    Camera Camera_GetR3D()
    {
        if (view_camera != null && view_camera.Camera_IsValid())
        {
            return view_camera.Camera_GetData();
        }
        Camera rcam = R3D.CameraFromRL(camera);
        rcam.NearPlane = 0.05f;
        rcam.FarPlane = 500f;
        return rcam;
    }

    public Ray Trace_Ray(Vector2 screen)
    {
        Camera3D cam = Camera_GetRL();
        if (transpose_traces)
        {
            return ImpGizmo.ScreenToRay3(screen, cam, Dimensions_Get());
        }
        if (ImpApp.app != null)
        {
            return Raylib.GetScreenToWorldRay(screen, ImpApp.app.camera);
        }
        return ImpGizmo.ScreenToRay3(screen, cam, Dimensions_Get());
    }

    public Imp3D Trace_Pick(Vector2 screen, out Vector3 hit)
    {
        ImpComp src = Root_Get();
        if (src == null)
        {
            hit = default;
            return null;
        }
        return Imp3D.Select(src, Trace_Ray(screen), out hit);
    }

    public bool Trace_World(Vector2 screen, out Vector3 pos, out Imp3D hit)
    {
        hit = Trace_Pick(screen, out pos);
        if (hit != null)
        {
            return true;
        }
        return Imp3D.Ray_Plane(Trace_Ray(screen), Vector3.Zero, Vector3.UnitY, out pos);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);

        TDimensions2 dim = Dimensions_Get();
        int w = Math.Max(1, (int)MathF.Round(dim.size.X));
        int h = Math.Max(1, (int)MathF.Round(dim.size.Y));
        if (dim.size.X < 1 || dim.size.Y < 1)
        {
            return;
        }

        if (target.Id == 0 || Math.Abs(target_w - w) > 1 || Math.Abs(target_h - h) > 1)
        {
            double t_rt = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
            if (target.Id != 0)
            {
                Raylib.UnloadRenderTexture(target);
            }
            target = Raylib.LoadRenderTexture(w, h);
            target_w = w;
            target_h = h;
            if (ImpProfiler.enabled)
            {
                ImpProfiler.sv_rt += ImpProfiler.Now_Ms - t_rt;
            }
        }
        if (target.Id == 0)
        {
            return;
        }

        view_scene?.ApplyRenderState();
        R3D.SetAspectMode(AspectMode.Expand);
        Imp3D.Resolution_Sync(w, h);

        Camera rcam = Camera_GetR3D();

        View view = new()
        {
            Camera = rcam,
            Target = target,
            Viewport = new Rectangle(0, 0, w, h),
        };

        double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        A_Material.draw_stamp++;
        R3D.BeginPro(view);
        ImpComp src = Root_Get();
        if (src != null)
        {
            src.Draw(dt, draw_flags, 0);
        }
        overlay?.Draw(dt, draw_flags, 0);
        R3D.End();
        Raylib.EndScissorMode();
        Rlgl.SetBlendMode(Raylib_cs.BlendMode.Alpha);
        if (ImpProfiler.enabled)
        {
            ImpProfiler.sv_r3d += ImpProfiler.Now_Ms - t0;
        }

        double t_blit = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        Raylib.DrawTexturePro(
            target.Texture,
            new Rectangle(0, 0, w, -h),
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y),
            Vector2.Zero, 0f, Color.White);
        if (ImpProfiler.enabled)
        {
            ImpProfiler.sv_blit += ImpProfiler.Now_Ms - t_blit;
        }
    }
}
