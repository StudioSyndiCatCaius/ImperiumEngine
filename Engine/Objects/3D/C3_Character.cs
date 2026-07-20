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

    public A_MoveMode move_mode = new();

    R3D_cs.Model? _model;
    R3D_cs.Mesh?  _fallback;

    public override void OnInit()
    {
        string glbPath = Path.Combine(ImpAsset.s_engineContentDir, "3D", "sk_mannequin.glb");

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
        if (_model is R3D_cs.Model m) R3D.UnloadModel(m, true);
        _model    = null;
        _fallback = null;
    }
}
