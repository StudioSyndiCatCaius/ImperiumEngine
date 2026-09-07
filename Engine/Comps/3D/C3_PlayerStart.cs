using System.Numerics;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._3D;

//used to spawn a player at a specific position
public class C3_PlayerStart : Imp3D
{
    const float CAPSULE_RADIUS = 0.4f;
    const float CAPSULE_HEIGHT = 1.8f;
    static A_Texture _billboard;

    public static C3_PlayerStart GetFromId(int id, A_Scene scene = null)
    {
        C3_PlayerStart Find(A_Scene s)
        {
            foreach (ImpComp c in ImpComp.GetAllOfClass(new TClass<ImpComp>(typeof(C3_PlayerStart)), s))
            {
                if (c is C3_PlayerStart ps && ps.player_id == id) return ps;
            }
            return null;
        }

        if (scene != null) return Find(scene);
        C3_PlayerStart found = Find(App.scene_current);
        if (found != null) return found;
        if (App.scenes_global == null) return null;
        for (int i = 0; i < App.scenes_global.Count; i++)
        {
            found = Find(App.scenes_global[i]);
            if (found != null) return found;
        }
        return null;
    }

    [ImpVar] public int player_id;

    public override TBounds3 Bounds_Cache()
    {
        float rx = MathF.Abs(global_transform.scale.X) * CAPSULE_RADIUS;
        float ry = MathF.Abs(global_transform.scale.Y) * CAPSULE_RADIUS;
        float rz = MathF.Abs(global_transform.scale.Z) * CAPSULE_RADIUS;
        float hy = MathF.Abs(global_transform.scale.Y) * CAPSULE_HEIGHT;
        if (hy < ry * 2f) hy = ry * 2f;
        return new TBounds3
        {
            center = global_transform.position,
            size = new Vector3(rx * 2f, hy, rz * 2f),
            rotation = global_transform.rotation,
        };
    }

    public override void OnDrawDebug(double dt, bool drawing_3d)
    {
        base.OnDrawDebug(dt, drawing_3d);
        if (!drawing_3d) return;
        Color color = Color.SkyBlue;
        G3D.Draw3D_Capsule(global_transform, CAPSULE_RADIUS, CAPSULE_HEIGHT, 16, color);
        TTransform3 arrow = global_transform;
        arrow.rotation = GMath.Quat_2_Euler(
            GMath.Euler_2_Quat(global_transform.rotation)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI * 0.5f));
        G3D.Draw3D_Arrow(arrow, 0.8f, 0.04f, color);
        if (_billboard == null)
        {
            _billboard = new A_Texture();
            ImpFile src = GFile.Import<ImpFile>("{engine}/Editor/Types/Imp3D.png");
            if (src != null) _billboard.texture = src.get_Texture(0);
        }
        if (_billboard.texture.Id != 0)
            G3D.Draw3D_Billboard(_billboard, global_transform, 0.5f, Color.White);
    }
}