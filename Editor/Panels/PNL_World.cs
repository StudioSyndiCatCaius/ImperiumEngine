using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using R3D_cs;
using Raylib_cs;
using rlImGui_cs;
using static Raylib_cs.Raylib;

namespace Editor.Panels;

public enum ECameraViewMode
{
    Free,
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right,
}

public enum ECameraRenderMode
{
    Lit,
    Unlit,
    Wireframe,
}

public enum EWorldViewMode
{
    MODE_3D,
    MODE_2D,
    MODE_ALL, //view both 3d and 2d
}



// a panel that displays the 3d world
public class PNL_World : EditorPanel
{
    public A_Level? level;

    public EditorGizmo gizmo = new();

    //selected entities. Shared by reference with the outliner tree panel
    public List<ImpComponent> selection = new();
    public Action? on_selection_changed;

    public ECameraViewMode view_mode;
    public ECameraRenderMode render_mode;
    public EWorldViewMode world_view_mode; //mainly used for the level editor, but lets you toggle between 3d and 2d, editing 3d objects or 2d widgets

    // free-fly camera state — yaw/pitch in radians, rebuilt into a Camera3D each frame
    public Vector3 cam_position = new(8, 6, 8);
    public float cam_yaw = -2.35f;
    public float cam_pitch = -0.45f;
    public float cam_fov = 60f;
    public float move_speed = 8f;

    RenderTexture2D? _rt;
    Vector2 _content_size = new(1, 1); //viewport size measured during the ImGui pass, consumed next render
    Vector2 _mouse_local; //mouse position relative to the viewport image (measured during the ImGui pass)
    bool _viewport_hovered;

    public Camera3D BuildCamera()
    {
        Vector3 forward = new(
            MathF.Cos(cam_pitch) * MathF.Cos(cam_yaw),
            MathF.Sin(cam_pitch),
            MathF.Cos(cam_pitch) * MathF.Sin(cam_yaw));

        return new Camera3D
        {
            Position = cam_position,
            Target = cam_position + forward,
            Up = Vector3.UnitY,
            FovY = cam_fov,
            Projection = CameraProjection.Perspective,
        };
    }

    protected override void OnUpdate(double delta)
    {
        if (!_viewport_hovered) return;

        float wheel = GetMouseWheelMove();
        if (wheel != 0)
            move_speed = Math.Clamp(move_speed * (1f + wheel * 0.15f), 0.5f, 100f);

        // right-mouse: mouselook + WASD fly (Unreal-style)
        if (!IsMouseButtonDown(MouseButton.Right))
        {
            // gizmo hotkeys (only when not flying, so they don't clash with WASD)
            if (IsKeyPressed(KeyboardKey.W)) gizmo.mode = EGizmoMode.Translate;
            if (IsKeyPressed(KeyboardKey.E)) gizmo.mode = EGizmoMode.Rotate;
            if (IsKeyPressed(KeyboardKey.R)) gizmo.mode = EGizmoMode.Scale;
            if (IsKeyPressed(KeyboardKey.T))
                gizmo.space = gizmo.space == EGizmoSpace.World ? EGizmoSpace.Local : EGizmoSpace.World;
            return;
        }

        view_mode = ECameraViewMode.Free;

        var md = GetMouseDelta();
        cam_yaw += md.X * 0.0035f;
        cam_pitch = Math.Clamp(cam_pitch - md.Y * 0.0035f, -1.55f, 1.55f);

        Vector3 forward = new(
            MathF.Cos(cam_pitch) * MathF.Cos(cam_yaw),
            MathF.Sin(cam_pitch),
            MathF.Cos(cam_pitch) * MathF.Sin(cam_yaw));
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        Vector3 move = Vector3.Zero;
        if (IsKeyDown(KeyboardKey.W)) move += forward;
        if (IsKeyDown(KeyboardKey.S)) move -= forward;
        if (IsKeyDown(KeyboardKey.D)) move += right;
        if (IsKeyDown(KeyboardKey.A)) move -= right;
        if (IsKeyDown(KeyboardKey.E)) move += Vector3.UnitY;
        if (IsKeyDown(KeyboardKey.Q)) move -= Vector3.UnitY;

        if (move != Vector3.Zero)
        {
            float speed = move_speed * (IsKeyDown(KeyboardKey.LeftShift) ? 3f : 1f);
            cam_position += Vector3.Normalize(move) * speed * (float)delta;
        }
    }

    // renders the level into the viewport texture. Must run OUTSIDE the rlImGui frame,
    // before rlImGui.Begin(), since R3D drives its own framebuffers.
    public void RenderWorld(double delta)
    {
        if (level == null) return;

        int w = Math.Max(1, (int)_content_size.X);
        int h = Math.Max(1, (int)_content_size.Y);

        // R3D.SetResolution stalls, so only rebuild once a resize drag has settled
        bool size_changed = _rt == null || _rt.Value.Texture.Width != w || _rt.Value.Texture.Height != h;
        if (size_changed && (_rt == null || !IsMouseButtonDown(MouseButton.Left)))
        {
            if (_rt != null) UnloadRenderTexture(_rt.Value);
            _rt = LoadRenderTexture(w, h);
            R3D.SetResolution(w, h);
        }
        if (_rt == null) return;

        var cam = BuildCamera();

        gizmo.Update(cam, _mouse_local, _content_size,
            _viewport_hovered && !IsMouseButtonDown(MouseButton.Right));

        // click-picking — only when the click isn't grabbing a gizmo handle or flying
        if (_viewport_hovered && !gizmo.IsDragging && !gizmo.IsHovered
            && !IsMouseButtonDown(MouseButton.Right) && IsMouseButtonPressed(MouseButton.Left))
            PickEntity(cam);

        var view = new View
        {
            Camera = R3D.CameraFromRL(cam),
            Target = _rt.Value,
        };

        R3D.BeginPro(view);
        foreach (var c in level.components)
            c.Draw(delta, cam, EDrawFlags.EDITOR_DEBUG);
        R3D.End();

        // Debug/gizmo pass — raylib 3D mode (required by e.g. R3D.DrawLightShape)
        BeginTextureMode(_rt.Value);
        BeginMode3D(cam);
        foreach (var c in level.components)
            c.Draw(delta, cam, EDrawFlags.DEBUG_PASS | EDrawFlags.EDITOR_DEBUG);
        DrawSelectionBounds();
        gizmo.Draw(cam);
        EndMode3D();
        EndTextureMode();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        // toolbar
        ImGui.SetNextItemWidth(90);
        EnumCombo("##view_mode", ref view_mode);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        EnumCombo("##render_mode", ref render_mode); //TODO: unlit/wireframe not implemented yet
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        EnumCombo("##world_view_mode", ref world_view_mode);

        // gizmo mode buttons + world/local space toggle
        ImGui.SameLine();
        if (ImGui.RadioButton("Move", gizmo.mode == EGizmoMode.Translate)) gizmo.mode = EGizmoMode.Translate;
        ImGui.SameLine();
        if (ImGui.RadioButton("Rotate", gizmo.mode == EGizmoMode.Rotate)) gizmo.mode = EGizmoMode.Rotate;
        ImGui.SameLine();
        if (ImGui.RadioButton("Scale", gizmo.mode == EGizmoMode.Scale)) gizmo.mode = EGizmoMode.Scale;
        ImGui.SameLine();
        if (ImGui.Button(gizmo.space == EGizmoSpace.World ? "World" : "Local"))
            gizmo.space = gizmo.space == EGizmoSpace.World ? EGizmoSpace.Local : EGizmoSpace.World;

        ImGui.SameLine();
        ImGui.TextDisabled("RMB: fly | W/E/R: gizmo | T: space");

        _content_size = ImGui.GetContentRegionAvail();
        if (_rt != null)
        {
            rlImGui.ImageRenderTextureFit(_rt.Value, true);
            _viewport_hovered = ImGui.IsItemHovered() || gizmo.IsDragging;
            _mouse_local = ImGui.GetMousePos() - ImGui.GetItemRectMin();
        }
    }

    // ----------------------------------------------------------------
    // Selection
    // ----------------------------------------------------------------

    IEnumerable<ImpComponent> AllComponents()
    {
        if (level == null) yield break;
        var stack = new Stack<ImpComponent>(level.components);
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            yield return c;
            foreach (var child in c.Children) stack.Push(child);
        }
    }

    void PickEntity(Camera3D cam)
    {
        Ray ray = GetScreenToWorldRayEx(_mouse_local, cam, (int)_content_size.X, (int)_content_size.Y);

        ImpComponent3D? best = null;
        float best_d = float.MaxValue;
        foreach (var c in AllComponents())
            if (c is ImpComponent3D c3 && c3.is_visible && RayHitsBounds(ray, c3, out float d) && d < best_d)
            {
                best_d = d;
                best = c3;
            }

        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        if (best == null)
        {
            // clicked empty space: plain click deselects all, shift-click keeps selection
            if (shift || selection.Count == 0) return;
            selection.Clear();
        }
        else if (shift)
        {
            if (!selection.Remove(best)) selection.Add(best);
        }
        else
        {
            selection.Clear();
            selection.Add(best);
        }
        on_selection_changed?.Invoke();
    }

    // ray vs the component's oriented bounding box (local bounds through its transform)
    static bool RayHitsBounds(Ray ray, ImpComponent3D c, out float dist)
    {
        dist = 0;
        Matrix4x4 m = BoundsMatrix(c);
        if (!Matrix4x4.Invert(m, out var inv)) return false;

        var local = new Ray(
            Vector3.Transform(ray.Position, inv),
            Vector3.Normalize(Vector3.TransformNormal(ray.Direction, inv)));

        var box = c.GetLocalBounds();
        // inflate so flat bounds (e.g. planes) stay clickable
        box.Min -= new Vector3(0.05f);
        box.Max += new Vector3(0.05f);

        var col = GetRayCollisionBox(local, box);
        if (!col.Hit) return false;

        dist = Vector3.Distance(ray.Position, Vector3.Transform(col.Point, m));
        return true;
    }

    static Matrix4x4 BoundsMatrix(ImpComponent3D c) => c.WorldMatrix;

    // per-entity oriented boxes; when multiple are selected, a subtler box around them all
    void DrawSelectionBounds()
    {
        Color col_entity = new(255, 170, 40, 255);
        Color col_group = new(220, 220, 220, 110);

        Vector3 all_min = new(float.MaxValue);
        Vector3 all_max = new(float.MinValue);
        int drawn = 0;

        Span<Vector3> pts = stackalloc Vector3[8];
        foreach (var c in selection)
        {
            if (c is not ImpComponent3D c3) continue;

            var box = c3.GetLocalBounds();
            Matrix4x4 m = BoundsMatrix(c3);
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(
                    (i & 1) != 0 ? box.Max.X : box.Min.X,
                    (i & 2) != 0 ? box.Max.Y : box.Min.Y,
                    (i & 4) != 0 ? box.Max.Z : box.Min.Z);
                pts[i] = Vector3.Transform(p, m);
                all_min = Vector3.Min(all_min, pts[i]);
                all_max = Vector3.Max(all_max, pts[i]);
            }

            // 12 edges: corner pairs differing by exactly one axis bit
            for (int i = 0; i < 8; i++)
            for (int b = 1; b <= 4; b <<= 1)
            {
                int j = i | b;
                if (j > i) DrawLine3D(pts[i], pts[j], col_entity);
            }
            drawn++;
        }

        if (drawn > 1)
            DrawBoundingBox(new BoundingBox(all_min, all_max), col_group);
    }

    static void EnumCombo<T>(string label, ref T value) where T : struct, Enum
    {
        int current = Convert.ToInt32(value);
        string[] names = Enum.GetNames<T>();
        if (ImGui.Combo(label, ref current, names, names.Length))
            value = (T)Enum.ToObject(typeof(T), current);
    }

    protected override void OnClose()
    {
        if (_rt != null)
        {
            UnloadRenderTexture(_rt.Value);
            _rt = null;
        }
    }
}
