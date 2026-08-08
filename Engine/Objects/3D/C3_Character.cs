using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using R3D_cs;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace ImperiumEngine.Objects._3D;

public enum ECharacterType : byte
{
    Default,
    _2D,
    _3D,
}

public class C3_Character : C3_Collider
{
    public C3_Mesh     c_mesh     = new();
    public C3_Skeleton c_skeleton = new();
    public C3_Sprite   c_sprite   = new();

    [ImpVar] public ECharacterType type = ECharacterType.Default;

    [ImpVar] public A_MoveMode move_mode = new();

    R3D_cs.Model? _model;
    R3D_cs.Mesh?  _fallback;

    public C3_Character()
    {
        // an upright cylinder collider around the 1.8m mannequin; falls under gravity on play
        shape            = EColliderShape.Cylinder;
        radius           = 0.4f;
        height           = 1.8f;
        simulate_physics = true;
    }

    // stays upright like an Unreal capsule — the body can't tip over
    protected override bool LockUpright => true;

    public override void OnInit()
    {
        ReleaseVisuals();

        string glbPath = Path.Combine(ImpFile.s_engineContentDir, "3D", "sk_mannequin.glb");

        if (File.Exists(glbPath))
        {
            try { _model = R3D.LoadModel(glbPath); }
            catch
            {
                Console.WriteLine("[C3_Character] Failed to load mannequin — using placeholder");
                _fallback = R3D.GenMeshCylinder(0.4f, 1.8f, 12);
            }
        }
        else
        {
            _fallback = R3D.GenMeshCylinder(0.4f, 1.8f, 12);
        }
    }

    public override void OnDeinit()
    {
        ReleaseVisuals();
        base.OnDeinit();
    }

    public override void OnDraw(double delta, Camera3D cam, EDrawFlags flags)
    {
        if (flags.HasFlag(EDrawFlags.DEBUG_PASS)) return;

        GetWorldTRS(out var pos, out var rot, out var scale);

        if (_model is R3D_cs.Model model)
            R3D.DrawModelEx(model, pos, rot, scale);
        else if (_fallback is R3D_cs.Mesh mesh)
            R3D.DrawMeshEx(mesh, R3D.GetDefaultMaterial(), pos, rot, scale);
    }

    public override void OnEnd()
    {
        base.OnEnd(); // release the physics body (runtime only)
        ReleaseVisuals();
    }

    void ReleaseVisuals()
    {
        if (_model is R3D_cs.Model m) R3D.UnloadModel(m, true);
        _model    = null;
        _fallback = null;
    }
}
