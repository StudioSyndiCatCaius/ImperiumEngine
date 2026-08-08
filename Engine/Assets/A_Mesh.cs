using System.Numerics;
using ImperiumEngine.Main;
using R3D_cs;
using Raylib_cs;
using Mesh = R3D_cs.Mesh;

namespace ImperiumEngine.Assets;

public enum EMeshPrimitive
{
    None, Cube, Sphere, Plane, Capsule, Cylinder,
}

public class A_Mesh : ImpAsset
{
    // Built-in shape, generated on the GPU the first time the mesh is drawn. Assets that
    // import geometry off disc leave this at None and go through source_file instead.
    [ImpVar][Export] public EMeshPrimitive primitive = EMeshPrimitive.None;
    [ImpVar][Export] public Vector3 primitive_size = Vector3.One;

    Mesh r3d_mesh;
    bool is_built;

    public static A_Mesh Primitive(EMeshPrimitive shape, Vector3? size = null)
    {
        return new A_Mesh { primitive = shape, primitive_size = size ?? Vector3.One };
    }

    // The R3D mesh this asset draws with, built on first use. Building uploads vertex
    // buffers, so it is deferred until there is a window (and therefore a GL context) -
    // same reasoning as ImpAsset.Resource_Ensure.
    public Mesh? get_Mesh()
    {
        if (!is_built)
        {
            if (!Raylib.IsWindowReady()) return null;

            is_built = true;
            r3d_mesh = Mesh_Build();
        }

        return r3d_mesh.VertexCount > 0 ? r3d_mesh : null;
    }

    Mesh Mesh_Build()
    {
        var s = primitive_size;

        // Radii come from the size's diameter so every primitive reads as a bounding box.
        return primitive switch
        {
            EMeshPrimitive.Cube     => R3D.GenMeshCube(s.X, s.Y, s.Z),
            EMeshPrimitive.Sphere   => R3D.GenMeshSphere(s.X * 0.5f, 32, 16),
            EMeshPrimitive.Plane    => R3D.GenMeshPlane(s.X, s.Z, 1, 1),
            EMeshPrimitive.Capsule  => R3D.GenMeshCapsule(s.X * 0.5f, s.Y, 32, 8),
            EMeshPrimitive.Cylinder => R3D.GenMeshCylinder(s.X * 0.5f, s.Y, 32),
            _                       => default,
        };
    }

    public override string File_GetExtension()
    {
        return "ImpMesh";
    }
}
