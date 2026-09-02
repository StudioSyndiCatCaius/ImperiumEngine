using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Engine.Assets;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;
using Mesh = R3D_cs.Mesh;


namespace Engine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Mesh : Imp3D
{
    [ImpVar] public A_Mesh mesh;

    public C3_Mesh()
    {
        mesh=Imp.Asset_Load<A_Mesh>(A_Mesh.PATH_SHAPE_CUBE);
        cast_shadows=true;
    }
    
    
    public override TBounds3 Bounds_Cache()
    {
        if (mesh == null) return new();
        return TBounds3.Offset(mesh.GetBounds(), global_transform, true);
    }

    public override void OnBegin()
    {
        base.OnBegin();
    }

    public override void OnDraw3D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw3D(dt, flags);
        if (mesh != null) mesh.Draw(global_transform, cast_shadows);
    }
    
}