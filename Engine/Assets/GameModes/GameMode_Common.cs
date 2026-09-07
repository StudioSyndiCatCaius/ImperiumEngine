using System.Numerics;
using Engine;
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
    public TClass<Imp3D> GetPawnClass()
    {
        if(player_pawn.Get()!=null) return player_pawn;
        return GameMode_Common.default_pawn;
    }

    public TClass<C3_Camera> GetCameraClass()
    {
        if (default_camera.Get() != null) return default_camera;
        return new TClass<C3_Camera>(typeof(C3_Camera));
    }
    
    [ImpVar][Category("Player")] public TClass<Imp3D> player_pawn;
    [ImpVar][Category("Player")] public bool pawn_can_move_Forward=true;
    [ImpVar][Category("Player")] public bool pawn_can_move_Sideways=true;
    
    [ImpVar][Category("Camera")] public TClass<C3_Camera> default_camera = new(typeof(C3_Camera));
    [ImpVar][Category("Camera")] public float cam_distance=10;
    [ImpVar][Category("Camera")] public float cam_height=1.6f;
    [ImpVar][Category("Camera")] public float cam_fov=80;
    [ImpVar][Category("Camera")] public Vector3 cam_start_rotation=new(0,0,0);
    [ImpVar][Category("Camera")] public bool cam_start_rotation_is_global=false; //if the starting rotation is global or local to the player
    [ImpVar][Category("Camera")] public bool cam_can_rotate_H=false;
    [ImpVar][Category("Camera")] public bool cam_can_rotate_V=false;

    public override void OnPlayerStart(ImpPlayer player, TTransform3 start, A_Scene scene, A_GameMode instance)
    {
        TClass<Imp3D> pawnc = GetPawnClass();
        if (pawnc.Get() == null) return;
        Imp3D new_pawn = pawnc.Spawn(scene.root, (c) =>
        {
            Imp3D c3 = c as Imp3D;
            c3.Transform_Set(start);
        }) as Imp3D;
        if (new_pawn == null) return;

        player.pawn = new_pawn;
        player.target_3d = new_pawn;
        player.control_rotation = start.rotation;
        if (!player.input_targets.Contains(new_pawn))
            player.input_targets.Add(new_pawn);

        C3_Camera pawn_cam = GetCameraClass().Spawn(new_pawn, (c) =>
        {
            C3_Camera cam = c as C3_Camera;
            cam.fov = cam_fov;
            cam.camera_boom.length = cam_distance;
            cam.Position_Set(new Vector3(0, cam_height, 0), false);
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
    [ImpVar][Config] public static TClass<Imp3D> default_pawn= new TClass<Imp3D>(typeof(C3_Character));
    
    
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