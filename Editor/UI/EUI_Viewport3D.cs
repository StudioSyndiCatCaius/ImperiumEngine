using System.Numerics;
using Editor.Windows;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using ImGuiNET;
using R3D_cs;
using Raylib_cs;
using Camera = R3D_cs.Camera;

namespace Editor.UI;

public class EUI_Viewport3D : EdUi
{
    public A_Scene? scene;
    public ImpComp? selected;
    public Action<ImpComp?>? on_select;
    public EEditorGizmo_Mode gizmo_mode;
    public EEditorGizmo_Orientation gizmo_orientation;
    public EUI_Gizmo3D gizmo = new();

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
    public float look_dist = 11f;
    bool drag_look;
    bool drag_pan;
    bool drag_orbit;
    bool pending_pick;
    Vector2 pick_pos;
    public bool hovered;
    public bool IsCameraBusy => drag_look || drag_pan || drag_orbit;

    public override void OnDraw()
    {
        base.OnDraw();
        Vector2 avail = ImGui.GetContentRegionAvail();
        int w = Math.Max(1, (int)MathF.Round(avail.X));
        int h = Math.Max(1, (int)MathF.Round(avail.Y));
        if (avail.X < 1 || avail.Y < 1) return;

        EnsureTarget(w, h);
        Render(w, h);

        Vector2 pos = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##vp3d", new Vector2(w, h));
        hovered = ImGui.IsItemHovered();
        ImGui.GetWindowDrawList().AddImage(
            new IntPtr(target.Texture.Id),
            pos, pos + new Vector2(w, h),
            new Vector2(0, 1), new Vector2(1, 0));

        gizmo.mode = gizmo_mode;
        gizmo.orientation = gizmo_orientation;
        Imp3D? giz_target = selected is Imp3D o3 && !o3.Editor_IsLocked() ? o3 : null;
        bool giz = gizmo.OnDraw(giz_target, camera, pos, w, h, hovered);
        HandleInput(w, h, hovered, giz);
    }

    public void Focus(ImpComp? comp)
    {
        if (comp is not Imp3D o3) return;
        Vector3 t = o3.global_transform.position;
        if (look_dist < 0.5f) look_dist = 5f;
        Vector3 fwd = CamFwd();
        camera.Target = t;
        camera.Position = t - fwd * look_dist;
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

        Camera rcam = R3D.CameraFromRL(camera);
        rcam.NearPlane = 0.05f;
        rcam.FarPlane = 500f;
        rcam.CullMask = Layer.All;

        View view = new()
        {
            Camera = rcam,
            Target = target,
            Viewport = new Rectangle(0, 0, w, h),
        };

        Raylib.EndMode2D();
        R3D.SetAspectMode(AspectMode.Expand);
        R3D.BeginPro(view);
        if (scene?.root != null)
            DrawTree(scene.root, Raylib.GetFrameTime(), 0, EDrawFlags.Editor);
        R3D.End();
        Rlgl.SetBlendMode(Raylib_cs.BlendMode.Alpha);
        Raylib.BeginMode2D(App.camera_2d);
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

    void HandleInput(int w, int h, bool item_hovered, bool gizmo_block)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        bool alt = io.KeyAlt;
        bool rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        bool mmb = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
        bool lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);

        if (item_hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) drag_look = true;
        if (item_hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) drag_pan = true;
        if (item_hovered && alt && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) drag_orbit = true;
        if (item_hovered && !alt && !gizmo_block && !gizmo.busy && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pending_pick = true;
            pick_pos = io.MousePos;
        }
        if (gizmo_block || gizmo.busy) pending_pick = false;

        if (!rmb) drag_look = false;
        if (!mmb) drag_pan = false;
        if (!lmb) drag_orbit = false;

        Vector2 delta = io.MouseDelta;
        if (drag_look)
            Look(delta);
        else if (drag_orbit)
            Orbit(delta);
        else if (drag_pan)
            Pan(delta);

        if (drag_look)
        {
            float dt = Raylib.GetFrameTime();
            float speed = 8f * dt * (io.KeyShift ? 3f : 1f);
            Vector3 fwd = CamFwd();
            Vector3 right = CamRight();
            Vector3 move = Vector3.Zero;
            if (ImGui.IsKeyDown(ImGuiKey.W)) move += fwd;
            if (ImGui.IsKeyDown(ImGuiKey.S)) move -= fwd;
            if (ImGui.IsKeyDown(ImGuiKey.D)) move += right;
            if (ImGui.IsKeyDown(ImGuiKey.A)) move -= right;
            if (ImGui.IsKeyDown(ImGuiKey.E)) move += camera.Up;
            if (ImGui.IsKeyDown(ImGuiKey.Q)) move -= camera.Up;
            if (move != Vector3.Zero)
            {
                move = Vector3.Normalize(move) * speed * look_dist * 0.15f;
                camera.Position += move;
                camera.Target += move;
            }
        }

        if (item_hovered && io.MouseWheel != 0f)
        {
            Vector3 fwd = CamFwd();
            look_dist = Math.Clamp(look_dist * MathF.Pow(0.85f, io.MouseWheel), 0.2f, 10000f);
            camera.Position = camera.Target - fwd * look_dist;
        }

        if (pending_pick && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            pending_pick = false;
            if (!alt && Vector2.Distance(io.MousePos, pick_pos) < 4f)
                Pick(io.MousePos, w, h);
        }

        look_dist = Vector3.Distance(camera.Position, camera.Target);
        if (look_dist < 0.01f) look_dist = 0.01f;
    }

    void Look(Vector2 delta)
    {
        Vector3 fwd = CamFwd();
        Vector3 right = CamRight();
        float yaw = -delta.X * 0.005f;
        float pitch = -delta.Y * 0.005f;
        Quaternion qyaw = Quaternion.CreateFromAxisAngle(camera.Up, yaw);
        fwd = Vector3.Transform(fwd, qyaw);
        right = Vector3.Transform(right, qyaw);
        Quaternion qpitch = Quaternion.CreateFromAxisAngle(right, pitch);
        Vector3 next = Vector3.Transform(fwd, qpitch);
        if (MathF.Abs(Vector3.Dot(next, camera.Up)) < 0.999f) fwd = next;
        camera.Target = camera.Position + fwd * look_dist;
    }

    void Orbit(Vector2 delta)
    {
        Vector3 offset = camera.Position - camera.Target;
        float yaw = -delta.X * 0.005f;
        float pitch = -delta.Y * 0.005f;
        offset = Vector3.Transform(offset, Quaternion.CreateFromAxisAngle(camera.Up, yaw));
        Vector3 right = Vector3.Normalize(Vector3.Cross(camera.Up, offset));
        Vector3 pitched = Vector3.Transform(offset, Quaternion.CreateFromAxisAngle(right, pitch));
        if (MathF.Abs(Vector3.Dot(Vector3.Normalize(pitched), camera.Up)) < 0.999f) offset = pitched;
        camera.Position = camera.Target + offset;
        look_dist = offset.Length();
    }

    void Pan(Vector2 delta)
    {
        float scale = look_dist * 0.0015f;
        Vector3 right = CamRight();
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, CamFwd()));
        Vector3 move = right * (-delta.X * scale) + up * (delta.Y * scale);
        camera.Position += move;
        camera.Target += move;
    }

    void Pick(Vector2 mouse, int w, int h)
    {
        if (scene?.root == null) return;
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 local = mouse - min;
        Ray ray = Raylib.GetScreenToWorldRayEx(local, camera, w, h);
        ImpComp? best = null;
        float best_t = float.MaxValue;
        PickWalk(scene.root, ray, ref best, ref best_t);
        on_select?.Invoke(best);
    }

    void PickWalk(ImpComp c, Ray ray, ref ImpComp? best, ref float best_t)
    {
        if (c == null || !c.is_visible || c.Editor_IsLocked()) return;
        if (c is Imp3D o3)
        {
            TBounds3 b = o3.bounds.IsEmpty ? o3.Bounds_Cache() : o3.bounds;
            if (!b.IsEmpty && b.Raycast(ray.Position, ray.Direction, out float t) && t < best_t)
            {
                best_t = t;
                best = c;
            }
        }
        for (int i = 0; i < c.children.Count; i++)
            PickWalk(c.children[i], ray, ref best, ref best_t);
    }

    Vector3 CamFwd()
    {
        Vector3 f = camera.Target - camera.Position;
        return f.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(f);
    }

    Vector3 CamRight()
    {
        Vector3 r = Vector3.Cross(CamFwd(), camera.Up);
        return r.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(r);
    }
}
