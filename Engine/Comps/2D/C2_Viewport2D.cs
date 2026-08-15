using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

/// <summary>
/// Draws 2D comps from a scene and/or root. Optional transpose_traces remaps
/// screen picks through this widget's camera instead of raw screen space.
/// </summary>
public class C2_Viewport2D : Imp2D
{
    public ImpScene? view_scene;
    public ImpComp? root;
    public ImpComp? overlay;
    public bool transpose_traces = true;

    public TCamera2D camera = new()
    {
        position = new Vector2(960f, 540f),
        zoom = 0.5f,
    };

    RenderTexture2D target;
    int target_w;
    int target_h;

    public C2_Viewport2D()
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

    public Vector2 CanvasSize()
    {
        if (view_scene != null && view_scene.canvas_size.X > 1 && view_scene.canvas_size.Y > 1)
        {
            return view_scene.canvas_size;
        }
        return new Vector2(1920, 1080);
    }

    public Vector2 Trace_World(Vector2 screen)
    {
        if (transpose_traces)
        {
            return ImpGizmo.ScreenToWorld(screen, camera, Dimensions_Get());
        }
        return screen;
    }

    public Imp2D Trace_Pick(Vector2 screen)
    {
        ImpComp src = Root_Get();
        if (src == null)
        {
            return null;
        }
        return C2_Gizmo.Pick(src, camera, Dimensions_Get(), screen);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        ImpComp src = Root_Get();
        if (src != null)
        {
            Imp2D.SceneLayout_Set(src, CanvasSize());
        }
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
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
            if (target.Id != 0)
            {
                Raylib.UnloadRenderTexture(target);
            }
            target = Raylib.LoadRenderTexture(w, h);
            target_w = w;
            target_h = h;
        }
        if (target.Id == 0)
        {
            return;
        }

        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(new Color(18, 20, 24, 255));
        Vector2 view = new(w, h);
        Vector2 canvas = CanvasSize();
        Vector2 a = ImpGizmo.WorldToView(Vector2.Zero, camera, view);
        Vector2 b = ImpGizmo.WorldToView(new Vector2(canvas.X, 0), camera, view);
        Vector2 c = ImpGizmo.WorldToView(canvas, camera, view);
        Vector2 d = ImpGizmo.WorldToView(new Vector2(0, canvas.Y), camera, view);
        Color fill = new Color(26, 30, 36, 255);
        if (view_scene != null)
        {
            fill = ImpGizmo.WithAlpha(view_scene.background_color, 255);
        }
        Raylib.DrawTriangle(a, b, c, fill);
        Raylib.DrawTriangle(a, c, b, fill);
        Raylib.DrawTriangle(a, c, d, fill);
        Raylib.DrawTriangle(a, d, c, fill);

        ImpComp src = Root_Get();
        if (src != null)
        {
            Imp2D.SceneLayout_Set(src, canvas);
            Imp2D.SceneDraw_Begin(camera, view);
            src.Draw(dt, WDrawFlags.Editor, 1);
            overlay?.Draw(dt, WDrawFlags.Editor, 1);
            Imp2D.SceneDraw_End();
        }
        Raylib.EndTextureMode();

        Raylib.DrawTexturePro(
            target.Texture,
            new Rectangle(0, 0, w, -h),
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y),
            Vector2.Zero, 0f, Color.White);
    }
}
