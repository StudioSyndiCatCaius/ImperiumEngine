using System.Numerics;
using Engine;
using Engine.Comps._1D;
using Engine.Comps._1D.Systems;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets.GameModes;

[Title("Game Mode : Common")]
public class GameMode_Common : A_GameMode
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public TClass<Imp3D> GetPawnClass() { if(player_pawn.Get()!=null) return player_pawn; return GameMode_Common.default_pawn; }
    public TClass<C3_Camera> GetCameraClass() { if (default_camera.Get() != null) return default_camera; return new TClass<C3_Camera>(typeof(C3_Camera)); }
    
    public TClass<C1_GameSystem> GetRootSystemClass() { if(override_root_system.Get()!=null) return override_root_system; return GameMode_Common.default_root_system; }
    
    [ImpVar][Category("Player")] public TClass<Imp3D> player_pawn;
    [ImpVar][Category("Player")] public bool pawn_can_move_Forward=true;
    [ImpVar][Category("Player")] public bool pawn_can_move_Sideways=true;
    
    [ImpVar][Category("Camera")] public TClass<C3_Camera> default_camera = new(typeof(C3_Camera));
    [ImpVar][Category("Camera")] public float cam_distance=3;
    [ImpVar][Category("Camera")] public float cam_height=1.6f;
    [ImpVar][Category("Camera")] public float cam_fov=70;
    [ImpVar][Category("Camera")] public Vector3 cam_start_rotation=new(0,0,0);
    [ImpVar][Category("Camera")] public bool cam_start_rotation_is_global=false; //if the starting rotation is global or local to the player
    [ImpVar][Category("Camera")] public bool cam_can_rotate_H=false;
    [ImpVar][Category("Camera")] public bool cam_can_rotate_V=false;

    [ImpVar][Category("Systems")] public TClass<C1_GameSystem> override_root_system;

    //used in instance
    public float _update_sys_freq = 0.2f;
    public float _update_sys_time;
    
    
    public override void OnUpdate(A_Scene scene, A_GameMode instance, double delta)
    {
        base.OnUpdate(scene, instance, delta);
        if (_update_sys_time <= 0)
        {
            _update_sys_time = _update_sys_freq;
            C1_GameSystem.Activate(GetRootSystemClass());
        }
        else _update_sys_time -= (float)delta;

        foreach (var p in App.players)
        {
            if (p.camera != null)
            {
                p.control_rotation=p.camera.global_transform.rotation;
            }
        }
    }

    public override void OnPlayerStart(ImpPlayer player, TTransform3 start, A_Scene scene, A_GameMode instance)
    {
        TClass<Imp3D> pawnc = GetPawnClass();
        if (pawnc.Get() == null) return;
        
        //spawn player pawn
        Imp3D new_pawn = pawnc.Spawn(scene.root, (c) =>
        {
            Imp3D c3 = c as Imp3D;
            c3.Transform_Set(start);
        }) as Imp3D;
        if (new_pawn == null) return;

        //setup player pawn
        player.pawn = new_pawn;
        player.target_3d = new_pawn;
        player.control_rotation = start.rotation;
        if (!player.input_targets.Contains(new_pawn))
            player.input_targets.Add(new_pawn);

        //spawn player camera
        C3_Camera pawn_cam = GetCameraClass().Spawn(scene.root, (c) =>
        {
            C3_Camera cam = c as C3_Camera;
            cam.fov = cam_fov;
            cam.camera_boom.length = cam_distance;
            cam.cam_target = new_pawn;
            cam.target_mode = ECameraTargetMode.Follow;
            cam.target_interp_speed = 15;
            cam.Position_Set(new Vector3(0, cam_height, 0), false);
            player.camera = cam;
            if (cam_start_rotation_is_global)
                cam.Rotation_Set(cam_start_rotation, true);
            else
                cam.Rotation_Set(cam_start_rotation, false);
        }) as C3_Camera;

        //THIS FUNCTION WORKS FOR NOW BUT IS BAD. players should eventually have their own view_target and system getting them
        if (player.id == 0 && pawn_cam != null)
            App.view_target = pawn_cam;
    }

    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Config] public static TClass<Imp3D> default_pawn= new (typeof(C3_Character));
    [ImpVar][Config] public static TClass<C1_GameSystem> default_root_system=new (typeof(sys_Explore));
    
    [Builtin] public static GameMode_Common THIRD_PERSON = new()
    {
        cam_can_rotate_H = true,
        cam_can_rotate_V = true,
    };
    
    [Builtin] public static GameMode_Common FIRST_PERSON = new()
    {
        cam_can_rotate_H = false,
        cam_can_rotate_V = false,
        cam_distance = 0,
    };
    
    [Builtin] public static GameMode_Common SIDE_SCROLLER = new()
    {
        cam_start_rotation = new(0,0,0)
    };
    
    
    [Builtin] public static GameMode_Common TOP_DOWN_DIRECT = new()
    {
        
    };
    
    [Builtin] public static GameMode_Common TOP_DOWN_ANGLED = new()
    {
        
    };
    [Builtin] public static GameMode_Common TOP_DOWN_ISOMETRIC = new()
    {
        
    };
    
    [Builtin] public static GameMode_Common TOP_DOWN_ROTATABLE = new()
    {
        cam_can_rotate_H = true,
    };
}