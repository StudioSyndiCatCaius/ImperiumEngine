using System.Numerics;
using Editor.Windows;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;

namespace Editor.UI;

public class EUI_Viewport2D : EdUi
{
    public A_Scene? scene;
    public ImpComp? selected;
    public Action<ImpComp?>? on_select;
    public EEditorGizmo_Mode gizmo_mode;
    public EEditorGizmo_Orientation gizmo_orientation;
    public EUI_Gizmo2D gizmo = new();

    public const float CanvasW = 1920f;
    public const float CanvasH = 1080f;

    public Camera2D camera = new()
    {
        Target = new Vector2(CanvasW * 0.5f, CanvasH * 0.5f),
        Offset = Vector2.Zero,
        Rotation = 0,
        Zoom = 0.5f,
    };

    RenderTexture2D target;
    int target_w;
    int target_h;
    bool drag_pan;
    bool pending_pick;
    Vector2 pick_pos;
    Vector2 vp_min;
    public bool hovered;

    public override void OnDraw()
    {
        base.OnDraw();
        Vector2 avail = ImGui.GetContentRegionAvail();
        int w = Math.Max(1, (int)avail.X);
        int h = Math.Max(1, (int)avail.Y);
        if (avail.X < 1 || avail.Y < 1) return;

        if (scene != null && scene.viewport == null)
            scene.viewport = new ImpViewport { size = new Vector2(CanvasW, CanvasH) };

        camera.Offset = new Vector2(w * 0.5f, h * 0.5f);
        EnsureTarget(w, h);
        Render(w, h);

        vp_min = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##vp2d", new Vector2(w, h));
        hovered = ImGui.IsItemHovered();
        ImGui.GetWindowDrawList().AddImage(
            new IntPtr(target.Texture.Id),
            vp_min, vp_min + new Vector2(w, h),
            new Vector2(0, 1), new Vector2(1, 0));

        gizmo.mode = gizmo_mode;
        gizmo.orientation = gizmo_orientation;
        Imp2D? giz_target = selected is Imp2D o2 && !o2.Editor_IsLocked() ? o2 : null;
        bool giz = gizmo.OnDraw(giz_target, camera, vp_min, w, h, hovered);
        HandleInput(hovered, giz);
    }

    public void Focus(ImpComp? comp)
    {
        if (comp is not Imp2D o2) return;
        camera.Target = (o2.bounds.start + o2.bounds.end) * 0.5f;
    }

    void EnsureTarget(int w, int h)
    {
        if (target.Id != 0 && Math.Abs(target_w - w) <= 1 && Math.Abs(target_h - h) <= 1)
            return;
        if (target.Id != 0)
            Raylib.UnloadRenderTexture(target);
        target = Raylib.LoadRenderTexture(w, h);
        target_w = w;
        target_h = h;
    }

    void Render(int w, int h)
    {
        if (target.Id == 0) return;
        Raylib.EndMode2D();
        Raylib.BeginTextureMode(target);
        Rlgl.DisableDepthTest();
        Rlgl.EnableShader(Rlgl.GetShaderIdDefault());
        Rlgl.SetBlendMode(BlendMode.Alpha);
        Rlgl.SetTexture(0);
        Raylib.ClearBackground(new Color(18, 20, 24, 255));
        Raylib.BeginMode2D(camera);

        DrawGrid(w, h);

        if (scene?.root != null)
            DrawTree(scene.root, Raylib.GetFrameTime(), 1, EDrawFlags.Editor);

        Raylib.EndMode2D();
        DrawScreenOverlays();
        Raylib.EndTextureMode();
        Raylib.BeginMode2D(App.camera_2d);
    }

    void DrawGrid(int w, int h)
    {
        Vector2 a = Raylib.GetScreenToWorld2D(Vector2.Zero, camera);
        Vector2 b = Raylib.GetScreenToWorld2D(new Vector2(w, h), camera);
        float x0w = MathF.Min(a.X, b.X);
        float x1w = MathF.Max(a.X, b.X);
        float y0w = MathF.Min(a.Y, b.Y);
        float y1w = MathF.Max(a.Y, b.Y);

        float step = 64f;
        float zoom = MathF.Max(0.001f, camera.Zoom);
        while (step * zoom < 10f) step *= 4f;

        int x0 = (int)MathF.Floor(x0w / step);
        int x1 = (int)MathF.Ceiling(x1w / step);
        int y0 = (int)MathF.Floor(y0w / step);
        int y1 = (int)MathF.Ceiling(y1w / step);
        float thick = 1f / zoom;
        Color minor = new(32, 36, 42, 255);
        Color major = new(48, 54, 64, 255);
        for (int x = x0; x <= x1; x++)
            Raylib.DrawLineEx(new Vector2(x * step, y0 * step), new Vector2(x * step, y1 * step), thick, x % 4 == 0 ? major : minor);
        for (int y = y0; y <= y1; y++)
            Raylib.DrawLineEx(new Vector2(x0 * step, y * step), new Vector2(x1 * step, y * step), thick, y % 4 == 0 ? major : minor);
    }

    void DrawScreenOverlays()
    {
        Vector2 s = Raylib.GetWorldToScreen2D(Vector2.Zero, camera);
        Vector2 e = Raylib.GetWorldToScreen2D(new Vector2(CanvasW, CanvasH), camera);
        Raylib.DrawRectangleLinesEx(
            new Rectangle(s.X, s.Y, e.X - s.X, e.Y - s.Y),
            1f, new Color(70, 80, 96, 255));
        if (scene?.root != null)
            DrawBoundsOverlay(scene.root);
        if (selected is Imp2D o2)
            DrawLayoutDots(o2);
    }

    void DrawLayoutDots(Imp2D o2)
    {
        TBounds2 slot;
        if (o2.parent is Imp2D p && !p.ChildLayout_IsFree())
        {
            int ind = (uint)o2.sibling_index < (uint)p.children.Count && p.children[o2.sibling_index] == o2
                ? o2.sibling_index
                : -1;
            slot = ind < 0 ? o2.bounds : p.Child_MakeBounds2D(o2, ind);
        }
        else if (o2.parent is Imp2D pp && !pp.bounds.IsEmpty)
            slot = pp.ContentBounds();
        else
            slot = o2.Viewport_Get().Bounds;

        Vector2 origin = new(
            MathF.Min(slot.start.X, slot.end.X),
            MathF.Min(slot.start.Y, slot.end.Y));
        Vector2 view = new(
            MathF.Abs(slot.end.X - slot.start.X),
            MathF.Abs(slot.end.Y - slot.start.Y));

        float Align(ELayoutAlignment a, float start, float size) => a switch
        {
            ELayoutAlignment.Center => start + size * 0.5f,
            ELayoutAlignment.End => start + size,
            _ => start,
        };

        Vector2 align_pt = new(
            Align(o2.layout.alignment.align_H, origin.X, view.X),
            Align(o2.layout.alignment.align_V, origin.Y, view.Y));
        Vector2 anchor_pt = o2.bounds.start + o2.layout.GetAnchorPosition();

        Vector2 ascr = Raylib.GetWorldToScreen2D(align_pt, camera);
        Vector2 nscr = Raylib.GetWorldToScreen2D(anchor_pt, camera);
        Raylib.DrawCircleV(ascr, 5f, new Color(255, 220, 70, 230));
        Raylib.DrawCircleLinesV(ascr, 7f, new Color(255, 220, 70, 180));
        Raylib.DrawCircleV(nscr, 4f, new Color(255, 110, 50, 240));
        Raylib.DrawCircleLinesV(nscr, 6f, new Color(255, 110, 50, 180));
    }

    void DrawBoundsOverlay(ImpComp c)
    {
        if (c == null || !c.is_visible) return;
        if (c is Imp2D o2 && !o2.bounds.IsEmpty)
        {
            Vector2 a = Raylib.GetWorldToScreen2D(o2.bounds.start, camera);
            Vector2 b = Raylib.GetWorldToScreen2D(o2.bounds.end, camera);
            Raylib.DrawRectangleLinesEx(
                new Rectangle(a.X, a.Y, b.X - a.X, b.Y - a.Y),
                1f, c == selected ? new Color(120, 180, 255, 200) : new Color(80, 110, 150, 120));
        }
        for (int i = 0; i < c.children.Count; i++)
            DrawBoundsOverlay(c.children[i]);
    }

    void DrawTree(ImpComp c, double dt, byte pass, EDrawFlags flags)
    {
        if (c == null || !c.is_visible) return;
        EDrawFlags f = flags;
        if (c == selected) f |= EDrawFlags.Selected;
        c.Draw(dt, pass, f);
        for (int i = 0; i < c.children.Count; i++)
            DrawTree(c.children[i], dt, pass, flags);
    }

    void HandleInput(bool item_hovered, bool gizmo_block)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        bool mmb = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
        bool rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        bool alt = io.KeyAlt;
        bool lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);

        if (item_hovered && (ImGui.IsMouseClicked(ImGuiMouseButton.Middle) || ImGui.IsMouseClicked(ImGuiMouseButton.Right) || (alt && ImGui.IsMouseClicked(ImGuiMouseButton.Left))))
            drag_pan = true;
        if (!mmb && !rmb && !(alt && lmb)) drag_pan = false;

        if (drag_pan)
            camera.Target -= io.MouseDelta / MathF.Max(0.001f, camera.Zoom);

        if (item_hovered && io.MouseWheel != 0f)
        {
            Vector2 mouse_local = io.MousePos - vp_min;
            Vector2 before = Raylib.GetScreenToWorld2D(mouse_local, camera);
            float wheel = Math.Clamp(io.MouseWheel, -4f, 4f);
            camera.Zoom = Math.Clamp(camera.Zoom * MathF.Pow(1.18f, wheel), 0.05f, 32f);
            Vector2 after = Raylib.GetScreenToWorld2D(mouse_local, camera);
            camera.Target += before - after;
        }

        if (item_hovered && !alt && !gizmo_block && !gizmo.busy && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pending_pick = true;
            pick_pos = io.MousePos;
        }
        if (gizmo_block || gizmo.busy) pending_pick = false;
        if (pending_pick && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            pending_pick = false;
            if (!alt && Vector2.Distance(io.MousePos, pick_pos) < 4f)
                Pick(io.MousePos);
        }
    }

    Vector2 ScreenToWorld(Vector2 mouse)
        => Raylib.GetScreenToWorld2D(mouse - vp_min, camera);

    void Pick(Vector2 mouse)
    {
        if (scene?.root == null) return;
        Vector2 world = ScreenToWorld(mouse);
        ImpComp? best = null;
        PickWalk(scene.root, world, ref best);
        on_select?.Invoke(best);
    }

    void PickWalk(ImpComp c, Vector2 world, ref ImpComp? best)
    {
        if (c == null || !c.is_visible || c.Editor_IsLocked()) return;
        for (int i = 0; i < c.children.Count; i++)
            PickWalk(c.children[i], world, ref best);
        if (c is Imp2D o2 && o2.Contains(world))
            best = c;
    }
}
