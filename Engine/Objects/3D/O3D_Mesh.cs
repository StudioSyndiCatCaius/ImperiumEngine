using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using R3D_cs;

namespace ImperiumEngine.Objects._3D;

public class O3D_Mesh : ImpPhysic3D
{
    public A_Mesh mesh = new();
    public List<A_Material> override_materials = new();

    // Serialized source path — applied to mesh.file_source in OnInit
    [ImpVar] public string mesh_source = "";

    R3D_cs.Mesh?  _mesh;
    R3D_cs.Model? _model;

    public override void OnInit()
    {
        // [ImpVar] fields are set before OnInit, so mesh_source is already populated
        mesh.file_source = mesh_source;

        string source = ImpAsset.ResolvePath(mesh.file_source);

        if (source == "builtin:plane")
        {
            _mesh = R3D.GenMeshPlane(1f, 1f, 8, 8);
        }
        else if (!string.IsNullOrEmpty(source) && File.Exists(source))
        {
            _model = R3D.LoadModel(source);
        }
        else if (!string.IsNullOrEmpty(source))
        {
            Console.WriteLine($"[O3D_Mesh] Source not found: {source}");
        }
    }

    public override void OnDraw(double delta, EDrawFlags flags)
    {
        var rot = Quaternion.CreateFromYawPitchRoll(
            transform.Rotation.Y * (MathF.PI / 180f),
            transform.Rotation.X * (MathF.PI / 180f),
            transform.Rotation.Z * (MathF.PI / 180f));

        if (_mesh is R3D_cs.Mesh m)
            R3D.DrawMeshEx(m, R3D.GetDefaultMaterial(), transform.Position, rot, transform.Scale);
        else if (_model is R3D_cs.Model model)
            R3D.DrawModelEx(model, transform.Position, rot, transform.Scale);
    }

    public override void OnEnd()
    {
        if (_model is R3D_cs.Model m) R3D.UnloadModel(m, true);
        _model = null;
        _mesh  = null;
    }
}
