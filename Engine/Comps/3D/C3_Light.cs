using Engine.Core;
using Engine.Globals;
using R3D_cs;
using Raylib_cs;

namespace Engine.Comps._3D;

public enum ELightType { Point, Spot }

public class C3_Light : Imp3D
{
    [ImpVar] public ELightType type;
    [ImpVar] public float intensity=1.0f;
    [ImpVar] public Color color=Color.White;
    [ImpVar] public float radius=5;

    Light light_id;
    public static readonly List<C3_Light> editor_radius = new();

    public override void OnInit()
    {
        base.OnInit();
        LightType lt = type == ELightType.Spot ? LightType.Spot : LightType.Omni;
        light_id = R3D.CreateLight(lt);
        if (R3D.IsLightValid(light_id))
        {
            R3D.EnableShadow(light_id);
            R3D.SetShadowUpdateMode(light_id, shadow_update_mode);
        }
        if (is_visible)
        {
            R3D.EnableLight(light_id);
        }
        else
        {
            R3D.DisableLight(light_id);
        }
        Apply();
    }

    public override void OnDeinit()
    {
        if (R3D.IsLightValid(light_id)) R3D.DestroyLight(light_id);
        base.OnDeinit();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Apply();
    }

    public override void OnDraw3D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw3D(dt, flags);
        if ((flags & EDrawFlags.Editor) != 0 && (flags & EDrawFlags.Selected) != 0)
            editor_radius.Add(this);
    }

    void Apply()
    {
        if (!R3D.IsLightValid(light_id)) return;
        R3D.SetLightPosition(light_id, global_transform.position);
        R3D.SetLightColor(light_id, color);
        R3D.SetLightEnergy(light_id, intensity);
        R3D.SetLightRange(light_id, radius);
        if (type == ELightType.Spot)
            R3D.SetLightDirection(light_id, GMath.V3_Rotate(GMath.WORLD_FORWARD, global_transform.rotation));
    }
}