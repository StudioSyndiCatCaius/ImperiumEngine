using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Objects._3D;

public abstract class O3D_Light : ImpComponent3D
{
    [ImpVar] public Color color        = Color.White;
    [ImpVar] public float intensity    = 1.0f;
    [ImpVar] public float range        = 10.0f;
    [ImpVar] public bool  cast_shadows = true;
    
    protected Light _light;
    
    protected virtual LightType GetLightType() => LightType.Omni;
    
    
    public O3D_Light()
    {
        _light = R3D.CreateLight(GetLightType());
    }

    bool? _shadowsApplied;

    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        R3D.SetLightPosition(_light, transform.Position);
        R3D.SetLightColor(_light, color);
        R3D.SetLightEnergy(_light, intensity);
        R3D.SetLightRange(_light, range);
        R3D.SetLightActive(_light, is_visible);

        // Shadow maps are allocated on enable — only toggle on change.
        if (_shadowsApplied != cast_shadows)
        {
            if (cast_shadows) R3D.EnableShadow(_light);
            else              R3D.DisableShadow(_light);
            _shadowsApplied = cast_shadows;
        }
    }

    public override void OnEnd()
    {
        base.OnEnd();
        R3D.SetLightActive(_light, false);
    }
}

// ============================================================
// Point / Omni
// ============================================================

public class O3D_LightPoint : O3D_Light
{
    
    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        //transform.Position+=Vector3.Normalize(new Vector3(0.00f, 0.001f, 0.01f));
    }

}

// ============================================================
// Directional — handled by O3D_Environment's sun.
// Stub kept for editor/asset compatibility.
// ============================================================

public class O3D_LightDirectional : O3D_Light { }

// ============================================================
// Spot
// ============================================================

public class O3D_LightSpot : O3D_Light
{
    [ImpVar] public float   inner_angle = 20f;
    [ImpVar] public float   outer_angle = 35f;
    [ImpVar] public Vector3 direction   = -Vector3.UnitY;

    protected override LightType GetLightType() => LightType.Spot;

    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        
        R3D.SetLightDirection(_light, direction);
        R3D.SetLightInnerCutOff(_light, inner_angle);
        R3D.SetLightOuterCutOff(_light, outer_angle);
    }

    public override void OnEnd() => R3D.SetLightActive(_light, false);
}
