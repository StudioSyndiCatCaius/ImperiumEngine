using ImperiumEngine.Main;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

public enum ELightType { Directional, Point, Spot }

// A light in the scene. R3D owns lights in a global registry rather than per-scene, so the
// handle is created on the first draw (by which point R3D is up) and the comp re-arms it
// every frame it draws. Imp3D.Lights_DisableAll switches the registry off at the start of
// each render session, which is what keeps one scene's lights out of another's viewport.
public class C3_Light : ImpComp3D
{
    [ImpVar][Export] public ELightType light_type = ELightType.Directional;
    [ImpVar][Export] public Color color = Color.White;
    [ImpVar][Export] public float energy = 1.0f;
    [ImpVar][Export] public float specular = 1.0f;

    //point and spot only
    [ImpVar][Export] public float range = 25.0f;
    [ImpVar][Export] public float spot_angle = 45.0f;

    [ImpVar][Export] public bool cast_shadows = true;

    Light handle;
    bool has_handle;

    public override void OnDraw(double dt, EDrawFlags flags)
    {
        base.OnDraw(dt, flags);
        if (!is_visible) return;

        if (!has_handle)
        {
            handle = Imp3D.Light_Create(Type_ToR3D(light_type));
            has_handle = true;

            if (cast_shadows) R3D.EnableShadow(handle);
        }

        var world = Transform_Get(true);

        R3D.SetLightActive(handle, true);
        R3D.SetLightColor(handle, color);
        R3D.SetLightEnergy(handle, energy);
        R3D.SetLightSpecular(handle, specular);
        R3D.SetLightDirection(handle, Imp3D.Direction_FromEuler(world.rotation));

        if (light_type != ELightType.Directional)
        {
            R3D.SetLightPosition(handle, world.position);
            R3D.SetLightRange(handle, range);
        }

        if (light_type == ELightType.Spot) R3D.SetLightOuterCutOff(handle, spot_angle);
    }

    public override void OnDeinit()
    {
        base.OnDeinit();

        if (!has_handle) return;
        Imp3D.Light_Destroy(handle);
        has_handle = false;
    }

    static LightType Type_ToR3D(ELightType type)
    {
        return type switch
        {
            ELightType.Point => LightType.Omni,
            ELightType.Spot  => LightType.Spot,
            _                => LightType.Dir,
        };
    }
}
