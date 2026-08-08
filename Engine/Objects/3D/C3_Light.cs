using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Objects._3D;

public abstract class C3_Light : ImpComponent3D
{
    [ImpVar] public Color color        = Color.White;
    [ImpVar] public float intensity    = 1.0f;
    [ImpVar] public float range        = 10.0f;
    [ImpVar] public bool  cast_shadows = true;
    
    protected Light _light;
    
    protected virtual LightType GetLightType() => LightType.Omni;
    
    Texture2D _billboard;
    
    public C3_Light()
    {
        _light = R3D.CreateLight(GetLightType());
        _billboard=Raylib.LoadTexture("D:\\PROJECTS\\ImperiumEngine\\GitRepo\\Engine\\Content\\2D\\icons\\t_ico_lightbulb.png");
    }

    bool? _shadowsApplied;

    // Construction: push light params into R3D so the editor preview (and pre-Begin runtime)
    // sees the light without needing OnUpdate.
    public override void OnInit()
    {
        base.OnInit();
        ApplyLight();
    }

    public override void OnDeinit()
    {
        R3D.SetLightActive(_light, false);
        _shadowsApplied = null;
        base.OnDeinit();
    }

    // Runtime tick — keeps the light in sync while playing.
    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        ApplyLight();
    }

    public override void OnDraw(double delta, Camera3D cam, EDrawFlags flags)
    {
        base.OnDraw(delta, cam, flags);

        // Editor has no OnUpdate: refresh light position/params each draw so gizmo moves
        // and inspector edits stay live in the viewport without a play session.
        if (flags.HasFlag(EDrawFlags.EDITOR_DEBUG))
            ApplyLight();

        if (flags.HasFlag(EDrawFlags.EDITOR_DEBUG))
        {
            Raylib.DrawBillboard(cam,_billboard,WorldPosition,1.0f,Color.White);
        }
        if (flags.HasFlag(EDrawFlags.EDITOR_SELECTED))
        {
            Raylib.DrawSphereWires(WorldPosition, range, 8,8,color);
        }
    }

    // Runtime play end — deactivate so a discarded PIE clone doesn't leave orphan lights.
    public override void OnEnd()
    {
        R3D.SetLightActive(_light, false);
        _shadowsApplied = null;
        base.OnEnd();
    }

    protected virtual void ApplyLight()
    {
        R3D.SetLightPosition(_light, WorldPosition);
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
}

// ============================================================
// Point / Omni
// ============================================================

public class C3_LightPoint : C3_Light
{
    
    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        //transform.Position+=Vector3.Normalize(new Vector3(0.00f, 0.001f, 0.01f));
    }

}

// ============================================================
// Directional — handled by C3_Environment's sun.
// Stub kept for editor/asset compatibility.
// ============================================================

public class C3_LightDirectional : C3_Light { }

// ============================================================
// Spot
// ============================================================

public class C3_LightSpot : C3_Light
{
    [ImpVar] public float   inner_angle = 20f;
    [ImpVar] public float   outer_angle = 35f;
    [ImpVar] public Vector3 direction   = -Vector3.UnitY;

    protected override LightType GetLightType() => LightType.Spot;

    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        ApplySpot();
    }

    protected override void ApplyLight()
    {
        base.ApplyLight();
        ApplySpot();
    }

    void ApplySpot()
    {
        R3D.SetLightDirection(_light, direction);
        R3D.SetLightInnerCutOff(_light, inner_angle);
        R3D.SetLightOuterCutOff(_light, outer_angle);
    }
}
