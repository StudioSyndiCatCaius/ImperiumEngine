using System.Numerics;
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
    Vector3 _applied_pos;
    Vector3 _applied_rot;
    Color _applied_color;
    float _applied_intensity;
    float _applied_radius;
    ELightType _applied_type;
    bool _applied;

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
        if (_applied
            && _applied_pos == global_transform.position
            && _applied_rot == global_transform.rotation
            && _applied_color.Equals(color)
            && _applied_intensity == intensity
            && _applied_radius == radius
            && _applied_type == type)
            return;
        _applied = true;
        _applied_pos = global_transform.position;
        _applied_rot = global_transform.rotation;
        _applied_color = color;
        _applied_intensity = intensity;
        _applied_radius = radius;
        _applied_type = type;
        R3D.SetLightPosition(light_id, global_transform.position);
        R3D.SetLightColor(light_id, color);
        R3D.SetLightEnergy(light_id, intensity);
        R3D.SetLightRange(light_id, radius);
        if (type == ELightType.Spot)
            R3D.SetLightDirection(light_id, Vector3.Transform(GMath.WORLD_FORWARD, world_rotation));
    }
}