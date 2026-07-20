using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using Tomlyn.Model;

namespace ImperiumEngine.Structs;

public struct TTransform3D : I_Serialize
{
    [ImpVar] public Vector3 Position;
    [ImpVar] public Vector3 Rotation;
    [ImpVar] public Vector3 Scale= Vector3.One;

    public TTransform3D()
    {
        Position = default;
        Rotation = default;
    }

    // Custom serialization (via I_Serialize) keeps the compact, hand-editable
    // position/rotation/scale form and omits axes left at their identity value.
    public readonly void File_WriteTo(TomlTable t)
    {
        if (Position != Vector3.Zero) t["position"] = ImpToml.Vec3Array(Position);
        if (Rotation != Vector3.Zero) t["rotation"] = ImpToml.Vec3Array(Rotation);
        if (Scale    != Vector3.One)  t["scale"]    = ImpToml.Vec3Array(Scale);
    }

    public void File_ReadFrom(TomlTable t)
    {
        if (t.TryGetValue("position", out object? p)) Position = ImpToml.ToVec3(p);
        if (t.TryGetValue("rotation", out object? r)) Rotation = ImpToml.ToVec3(r);
        if (t.TryGetValue("scale",    out object? s)) Scale    = ImpToml.ToVec3(s, Vector3.One);
    }
}

public struct TTransform2D : I_Serialize
{
    [ImpVar] public Vector2 Position;
    [ImpVar] public float Rotation;
    [ImpVar] public Vector2 Scale= Vector2.One;

    public TTransform2D()
    {
        Position = default;
        Rotation = default;
    }

    public readonly void File_WriteTo(TomlTable t)
    {
        if (Position != Vector2.Zero) t["position"] = ImpToml.Vec2Array(Position);
        if (Rotation != 0f)           t["rotation"] = (double)Rotation;
        if (Scale    != Vector2.One)  t["scale"]    = ImpToml.Vec2Array(Scale);
    }

    public void File_ReadFrom(TomlTable t)
    {
        if (t.TryGetValue("position", out object? p)) Position = ImpToml.ToVec2(p);
        if (t.TryGetValue("rotation", out object? r)) Rotation = Convert.ToSingle(r);
        if (t.TryGetValue("scale",    out object? s)) Scale    = ImpToml.ToVec2(s, Vector2.One);
    }
}
