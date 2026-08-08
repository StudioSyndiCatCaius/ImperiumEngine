using System.Numerics;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using R3D_cs;
using Raylib_cs;
using Camera = R3D_cs.Camera;

namespace ImperiumEngine.Comps._2D;

public enum ESceneViewMode { Only3D, Only2D, Both }

//a viewport for a scene
public class C2_SceneView : ImpComp2D
{
    // degrees of camera rotation per pixel of pointer motion
    const float ROTATE_SENSITIVITY = 0.15f;

    // pitching all the way to the pole flips the camera over, so stop just short
    const float PITCH_LIMIT = 89.0f;

    // one wheel notch scales the fly speed by this much
    const float SPEED_STEP = 1.15f;
    const float SPEED_MIN = 0.1f;
    const float SPEED_MAX = 1000.0f;

    // R3D's render resolution is global, so track what it is currently set to and only
    // pay for the resize when a viewport of a different size takes over.
    static int res_width;
    static int res_height;

    public ImpScene scene;
    public Camera camera = new Camera
    {
        Fovy = 60,
        NearPlane = 0.05,
        FarPlane = 4000,
        CullMask = Layer.All,
        Projection = Projection.Perspective,
    };
    public float camera_speed = 10.0f;

    public C3_Gizmo gizmo3D;
    public C2_Gizmo gizmo2D;
    
    public ESceneViewMode view_mode;
    public bool is_editor;
    public bool is_debug_view; //default true in an editor scene view. Can Toggle with "G"

    public bool input_enabled; //mainly for use in editor only

    public bool is_input_mode;

    // Orientation is kept as angles rather than read back off the camera each frame:
    // accumulating into a quaternion drifts, and pitch has to be clamped anyway.
    float camera_yaw;
    float camera_pitch;
    bool camera_framed;

    /*
     *  this determins things like whether scene elements do runtime functions like OnBegin/OnEnd/OnUpdate
     */
    public bool is_runtime=false;

    public C2_SceneView()
    {
        cursor_filter = ECursorFilter.Hit;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (is_runtime)
        {
            scene.Update(dt);
        }
        else if (is_editor && is_visible)
        {
            if (input_enabled)
            {
                if(ImpPlayer.Key_JustPressed(EInputKey.Key_G)) is_debug_view = !is_debug_view;

                /*
                 * right-mouse click-hold on viewport activates camera move mode
                 * WASD moves camera based on look, and rotating mouse rotates camera
                 * mouse wheel changes camera move speed
                 */

                // The viewport test only gates entering the mode: once the cursor is
                // captured it stops reporting a real position, so re-testing it would drop
                // straight back out of the mode on the next frame.
                if (ImpPlayer.Action_IsDown("_Aim") && (is_input_mode || Mouse_IsOver()))
                {
                    Input_Mode_Set(true);
                }
                else if (is_input_mode)
                {
                    Input_Mode_Set(false);
                }

                if (is_input_mode)
                {
                    Vector3 axis_cam_move = ImpPlayer.Action_GetAxis("_Move");
                    Vector3 axis_cam_rotate = ImpPlayer.Action_GetAxis("_Rotate");

                    if (axis_cam_move != Vector3.Zero)
                    {
                        //move camera along its own axes, so WASD follows where it is looking
                        Vector3 forward = R3D.GetCameraForward(camera);
                        Vector3 right = R3D.GetCameraRight(camera);

                        camera.Position += (forward * axis_cam_move.X + right * axis_cam_move.Z)
                                         * camera_speed * (float)dt;
                    }
                    if (axis_cam_rotate != Vector3.Zero)
                    {
                        //rotate camera. the axis carries pixels of pointer motion: X pitch, Y yaw.
                        //both subtract, so the world tracks the pointer rather than fighting it
                        camera_pitch -= axis_cam_rotate.X * ROTATE_SENSITIVITY;
                        camera_yaw   -= axis_cam_rotate.Y * ROTATE_SENSITIVITY;
                        Camera_Orient();
                    }

                    //wheel retunes the fly speed rather than dollying, as in most editors
                    if (ImpUI.wheel != 0f)
                    {
                        camera_speed = Math.Clamp(
                            camera_speed * MathF.Pow(SPEED_STEP, ImpUI.wheel), SPEED_MIN, SPEED_MAX);
                    }
                }
            }
            else if (is_input_mode)
            {
                Input_Mode_Set(false);
            }
        }
    }

    // ---------------------------------------------------
    // camera
    // ---------------------------------------------------

    // Points the camera at a spot in the world and re-derives the yaw/pitch it is flown
    // with, so the next mouse movement carries on from here instead of snapping.
    public void Camera_LookAt(Vector3 position, Vector3 target)
    {
        camera.Position = position;
        R3D.CameraLookAt(ref camera, target, Vector3.UnitY); //(target, up) - the camera's own position is taken from the struct

        Vector3 euler = Imp3D.Euler_FromQuat(camera.Rotation);
        camera_pitch = euler.X;
        camera_yaw = euler.Y;
        Camera_Orient();
    }

    void Camera_Orient()
    {
        camera_pitch = Math.Clamp(camera_pitch, -PITCH_LIMIT, PITCH_LIMIT);
        camera.Rotation = Imp3D.Quat_FromEuler(new Vector3(camera_pitch, camera_yaw, 0f));
    }

    // Initial framing waits for the first render: R3D.CameraLookAt is a native call, and
    // views are constructed while the editor shell is being built, before the window is up.
    void Camera_EnsureFramed()
    {
        if (camera_framed) return;
        camera_framed = true;

        Camera_LookAt(ImpApp.camera.Position, ImpApp.camera.Target);
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    void Input_Mode_Set(bool enabled)
    {
        if (is_input_mode == enabled) return;

        is_input_mode = enabled;
        if (enabled) Raylib.DisableCursor();
        else Raylib.EnableCursor();
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    // Renders the scene straight into this comp's rect. R3D.End() blits its result to the
    // area the view names, which is why this happens in the draw pass rather than up front:
    // by now the layout has settled and nothing else has painted over this rect yet.
    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        if (scene == null) return;
        if (rect.Width < 1f || rect.Height < 1f) return;
        if (view_mode == ESceneViewMode.Only2D) return;

        Camera_EnsureFramed();
        Resolution_Match();

        scene.Environment_Apply();
        Imp3D.Lights_DisableAll();

        //R3D drives the framebuffer itself, so any UI scissor has to stand down first
        ImpUI.Clip_Suspend();

        var view = new View
        {
            Camera = camera,
            Viewport = Imp3D.Viewport_FromScreen(rect),
        };

        R3D.BeginPro(view);
        scene.Draw(dt, Draw_Flags(flags));
        R3D.End();

        ImpUI.Clip_Resume();
    }

    // Sizing R3D's internal buffers to the viewport keeps the aspect ratio honest and
    // avoids rendering at one size only to rescale into another.
    void Resolution_Match()
    {
        int width = (int)rect.Width;
        int height = (int)rect.Height;
        if (width == res_width && height == res_height) return;

        R3D.SetResolution(width, height);
        res_width = width;
        res_height = height;
    }

    EDrawFlags Draw_Flags(EDrawFlags flags)
    {
        if (is_editor) flags |= EDrawFlags.WithEditor;
        if (is_debug_view) flags |= EDrawFlags.DebugDraw;
        return flags;
    }
}
