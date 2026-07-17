using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using R3D_cs;
using Tomlyn.Model;

namespace ImperiumEngine.Objects._3D;

public class O3D_Mesh : ImpPhysic3D
{
    public A_Mesh mesh = new();
    public List<A_Material> override_materials = new();

    public static O3D_Mesh FromToml(TomlTable entity)
    {
        var obj = new O3D_Mesh();
        if (entity.TryGetValue("transform", out object? tr) && tr is TomlTable trt)
            obj.transform = A_Level.ParseTransform(trt);
        if (entity.TryGetValue("mesh", out object? m) && m is TomlTable mt &&
            mt.TryGetValue("source", out object? src))
            obj.mesh.file_source = src!.ToString()!;
        return obj;
    }

    R3D_cs.Mesh?  _mesh;   // for builtin generated shapes
    R3D_cs.Model? _model;  // for file-loaded models

    public override void OnInit()
    {
        string source = ImpAsset.ResolvePath(mesh.file_source);

        if (source == "builtin:plane")
        {
            _mesh = R3D.GenMeshPlane(1f, 1f, 1, 1);
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

        if (_mesh is R3D_cs.Mesh mesh)
            R3D.DrawMeshEx(mesh, R3D.GetDefaultMaterial(), transform.Position, rot, transform.Scale);
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
