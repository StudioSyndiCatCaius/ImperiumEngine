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
    // Editor scene view sets Editor (G toggle). Game / standalone leave None.
    public EDrawFlags draw_flags;
    // Game view composites this over a 3D blit — skip the opaque canvas chrome.
    public bool clear_background = true;
    public bool draw_canvas = true;

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

        // Game view already blitted 3D into this rect. Draw HUD here instead of
        // through an RT — R3D.End can leave scissor/viewport that make the RT
        // opaque and cover the bottom of the 3D image.
        // Layout against this widget (same as standalone vs the window), and pass
        // dim.position as the scene-draw origin so view-local coords land in the
        // Game tab instead of the editor window's top-left.
        if (!clear_background && !draw_canvas)
        {
            Imp2D.Clip_Push(dim);
            ImpComp hud = Root_Get();
            if (hud != null)
            {
                Vector2 _view = new Vector2(w, h);
                Imp2D.SceneLayout_Set(hud, _view);
                Imp2D.SceneDraw_Begin(camera, _view, dim.position);
                hud.Draw(dt, draw_flags, 1);
                overlay?.Draw(dt, draw_flags, 1);
                Imp2D.SceneDraw_End();
            }
            Imp2D.Clip_Pop();
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
        if (clear_background)
        {
            Raylib.ClearBackground(new Color(18, 20, 24, 255));
        }
        else
        {
            Raylib.ClearBackground(Color.Blank);
        }
        Vector2 view = new(w, h);
        Vector2 canvas = CanvasSize();
        if (draw_canvas)
        {
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
        }

        ImpComp src = Root_Get();
        if (src != null)
        {
            Imp2D.SceneLayout_Set(src, canvas);
            Imp2D.SceneDraw_Begin(camera, view);
            src.Draw(dt, draw_flags, 1);
            overlay?.Draw(dt, draw_flags, 1);
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
